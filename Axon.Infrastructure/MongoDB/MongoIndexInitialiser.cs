using Axon.Core.Models;
using MongoDB.Driver;

namespace Axon.Infrastructure.MongoDB;

public static class MongoIndexInitialiser
{
    public static async Task InitialiseAsync(MongoContext context)
    {
        await CreateUserIndexesAsync(context);
        await CreateBlockIndexesAsync(context);
        await CreatePipelineIndexesAsync(context);
        await CreateDeliveryIndexesAsync(context);
    }

    private static async Task CreateUserIndexesAsync(MongoContext context)
    {
        var keys = Builders<User>.IndexKeys.Ascending(u => u.Email);
        var options = new CreateIndexOptions { Unique = true };
        await context.Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(keys, options));
    }

    private static async Task CreateBlockIndexesAsync(MongoContext context)
    {
        var roleIndex = Builders<BuildingBlock>.IndexKeys
            .Ascending(b => b.Role)
            .Ascending(b => b.SyncStatus)
            .Ascending(b => b.IsActive);
        await context.Blocks.Indexes.CreateOneAsync(new CreateIndexModel<BuildingBlock>(roleIndex));

        var artifactIndex = Builders<BuildingBlock>.IndexKeys
            .Ascending(b => b.ArtifactName)
            .Ascending(b => b.Version);
        await context.Blocks.Indexes.CreateOneAsync(new CreateIndexModel<BuildingBlock>(artifactIndex));
    }

    private static async Task CreatePipelineIndexesAsync(MongoContext context)
    {
        var keys = Builders<PipelineDefinition>.IndexKeys
            .Ascending(p => p.CreatedBy)
            .Ascending(p => p.Visibility);
        await context.Pipelines.Indexes.CreateOneAsync(new CreateIndexModel<PipelineDefinition>(keys));
    }

    private static async Task CreateDeliveryIndexesAsync(MongoContext context)
    {
        var keys = Builders<Delivery>.IndexKeys
            .Ascending(d => d.CreatedBy)
            .Ascending(d => d.Status)
            .Descending(d => d.CreatedAt);
        await context.Deliveries.Indexes.CreateOneAsync(new CreateIndexModel<Delivery>(keys));
    }
}
