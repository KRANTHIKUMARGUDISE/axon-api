using Axon.Core.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;

namespace Axon.Infrastructure.MongoDB;

public class MongoContext
{
    private readonly IMongoDatabase _database;

    static MongoContext()
    {
        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
        };
        ConventionRegistry.Register("axon", pack, _ => true);

        // Top-level documents: Id → "_id" as string
        foreach (var type in new[] { typeof(BuildingBlock), typeof(User), typeof(PipelineDefinition), typeof(Delivery), typeof(MigrationRun) })
        {
            var cm = new BsonClassMap(type);
            cm.AutoMap();
            cm.IdMemberMap.SetElementName("_id");
            BsonClassMap.RegisterClassMap(cm);
        }

        // Nested sub-documents: Id → "id" (camelCase, not "_id")
        foreach (var type in new[] { typeof(PipelineNode), typeof(PipelineEdge) })
        {
            var cm = new BsonClassMap(type);
            cm.AutoMap();
            cm.IdMemberMap.SetElementName("id");
            BsonClassMap.RegisterClassMap(cm);
        }
    }

    public MongoContext(IConfiguration configuration)
    {
        var uri = configuration["MongoDB:Uri"] ?? "mongodb://localhost:27017";
        var dbName = configuration["MongoDB:DatabaseName"] ?? "axon";
        var client = new MongoClient(uri);
        _database = client.GetDatabase(dbName);
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<BuildingBlock> Blocks => _database.GetCollection<BuildingBlock>("blocks");
    public IMongoCollection<PipelineDefinition> Pipelines => _database.GetCollection<PipelineDefinition>("pipelines");
    public IMongoCollection<Delivery> Deliveries => _database.GetCollection<Delivery>("deliveries");
    public IMongoCollection<MigrationRun> MigrationsRun => _database.GetCollection<MigrationRun>("migrations_run");

    public IMongoCollection<T> GetRawCollection<T>(string name) => _database.GetCollection<T>(name);
}
