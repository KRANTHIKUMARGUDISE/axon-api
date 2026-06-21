using System.Text;
using Axon.API.Hubs;
using Axon.API.Services;
using Axon.Core.Config;
using Axon.Core.Enums;
using Axon.Core.Interfaces;
using Axon.Core.Models;
using Axon.Core.Services;
using Axon.Infrastructure.MongoDB;
using Axon.Infrastructure.Repositories;
using Axon.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new Axon.API.Json.BsonDocumentJsonConverter());
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "Axon API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Enter your JWT token"
    });
    options.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});

// MongoDB
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<IUserRepository, MongoUserRepository>();
builder.Services.AddScoped<IBlockRepository, MongoBlockRepository>();
builder.Services.AddScoped<IPipelineRepository, MongoPipelineRepository>();
builder.Services.AddScoped<IDeliveryRepository, MongoDeliveryRepository>();

// SignalR
builder.Services.AddSignalR(options => { })
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
// In-memory backplane (dev). For production, swap with Redis:
// .AddStackExchangeRedis(connectionString)
builder.Services.AddScoped<IDeliveryHub, DeliveryHubService<DeliveryHub>>();

// Marketplace
builder.Services.Configure<MarketplaceConfig>(builder.Configuration.GetSection("Marketplace"));
builder.Services.AddHttpClient<IMarketplaceService, MarketplaceService>();
builder.Services.AddHostedService<MarketplaceSyncService>();

// Pipeline validator
builder.Services.AddScoped<PipelineValidator>();

// JWT
builder.Services.AddSingleton<JwtService>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "axon-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "axon-clients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        // SignalR sends the token via query string for WebSocket/SSE transports
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// CORS — AllowCredentials required for SignalR; axon-desktop://app for Electron client
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// Data migration (before indexes to transform existing docs)
using (var scope = app.Services.CreateScope())
{
    var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();
    await BlockSchemaMigration.ExecuteAsync(mongo);
}

// MongoDB indexes
using (var scope = app.Services.CreateScope())
{
    var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();
    await MongoIndexInitialiser.InitialiseAsync(mongo);
}

// Seed admin user
using (var scope = app.Services.CreateScope())
{
    var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var existing = await users.GetByEmailAsync("admin@axon.local");
    if (existing == null)
    {
        await users.CreateAsync(new User
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            Email = "admin@axon.local",
            DisplayName = "Admin",
            Team = "Platform",
            Role = UserRole.Admin,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("axon-admin"),
            CreatedAt = DateTime.UtcNow
        });
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<DeliveryHub>("/hubs/delivery");

app.Run();
