using Axon.Core.Enums;
using Axon.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Axon.Infrastructure.MongoDB;

public static class HumanGateSeedMigration
{
    private const string MigrationName = "seed_human_gate_block_v1";

    public static async Task ExecuteAsync(MongoContext context)
    {
        var existing = await context.MigrationsRun.Find(m => m.Name == MigrationName).FirstOrDefaultAsync();
        if (existing != null) return;

        const string artifactName = "human-gate";
        var existingBlock = await context.Blocks.Find(
            Builders<BuildingBlock>.Filter.Eq(b => b.ArtifactName, artifactName)
        ).FirstOrDefaultAsync();

        if (existingBlock == null)
        {
            var block = new BuildingBlock
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = "human-gate",
                Description = "Pauses the delivery for human review of the previous step's output. Approve to continue, reject to fail the delivery.",
                Role = BlockRole.Control,
                SourceType = SourceType.Axon,
                ArtifactName = artifactName,
                Version = 1,
                AgentRuntime = AgentRuntime.Axon,
                ArtifactFormat = ArtifactFormat.Native,
                CachedFiles = null,
                EntryPointPath = null,
                ContextRequirements = [],
                OutputSchema = [],
                Tags = ["system", "axon"],
                RunCount = 0,
                CreatedBy = "system",
                CreatedByName = "System",
                MarketplaceSource = null,
                MarketplacePath = null,
                MarketplaceVersion = null,
                SyncStatus = SyncStatus.Synced,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Blocks.InsertOneAsync(block);
        }

        await context.MigrationsRun.InsertOneAsync(new MigrationRun
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = MigrationName,
            RanAt = DateTime.UtcNow
        });
    }
}
