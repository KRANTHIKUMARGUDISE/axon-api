using Axon.Core.Enums;
using Axon.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Axon.Infrastructure.MongoDB;

public static class BlockSchemaMigration
{
    private const string MigrationName = "block_schema_refactor_b8";

    public static async Task ExecuteAsync(MongoContext context)
    {
        var existing = await context.MigrationsRun.Find(m => m.Name == MigrationName).FirstOrDefaultAsync();
        if (existing != null) return;

        await MigrateExistingBlocksAsync(context);
        await SeedAxonBlocksAsync(context);

        await context.MigrationsRun.InsertOneAsync(new MigrationRun
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = MigrationName,
            RanAt = DateTime.UtcNow
        });
    }

    private static async Task MigrateExistingBlocksAsync(MongoContext context)
    {
        var collection = context.GetRawCollection<BsonDocument>("blocks");
        var documents = await collection.Find(new BsonDocument()).ToListAsync();

        foreach (var doc in documents)
        {
            var id = doc["_id"].AsString;
            var name = doc.Contains("name") ? doc["name"].AsString : "";
            var artifactName = Slugify(name);

            var update = new BsonDocument();

            if (artifactName == "ticket-fetcher" || artifactName == "pr-creator")
            {
                update["sourceType"] = "Axon";
                update["artifactName"] = artifactName;
                update["agentRuntime"] = "Axon";
                update["artifactFormat"] = "Native";
                update["version"] = 1;
                update["cachedFiles"] = BsonNull.Value;
                update["entryPointPath"] = BsonNull.Value;
            }
            else
            {
                update["sourceType"] = "Local";
                update["artifactName"] = artifactName;
                update["agentRuntime"] = "Claude";
                update["artifactFormat"] = "Skill";
                update["version"] = 1;

                if (doc.Contains("cachedDefinition") && doc["cachedDefinition"].IsBsonNull == false)
                {
                    var definition = doc["cachedDefinition"].AsString;
                    var cachedFile = new BsonDocument
                    {
                        { "relativePath", "SKILL.md" },
                        { "content", definition }
                    };
                    update["cachedFiles"] = new BsonArray { cachedFile };
                    update["entryPointPath"] = "SKILL.md";
                }
                else
                {
                    update["cachedFiles"] = new BsonArray();
                    update["entryPointPath"] = BsonNull.Value;
                }
            }

            update["updatedAt"] = DateTime.UtcNow;

            var updateDef = new BsonDocument("$set", update);
            await collection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), updateDef);
        }
    }

    private static async Task SeedAxonBlocksAsync(MongoContext context)
    {
        var axonBlocks = new[]
        {
            new { Name = "ticket-fetcher", Role = BlockRole.IO },
            new { Name = "pr-creator", Role = BlockRole.Execution }
        };

        foreach (var blockDef in axonBlocks)
        {
            var artifactName = Slugify(blockDef.Name);
            var existing = await context.Blocks.Find(
                Builders<BuildingBlock>.Filter.Eq(b => b.ArtifactName, artifactName)
            ).FirstOrDefaultAsync();

            if (existing == null)
            {
                var newBlock = new BuildingBlock
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Name = blockDef.Name,
                    Description = $"System-seeded Axon block: {blockDef.Name}",
                    Role = blockDef.Role,
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
                await context.Blocks.InsertOneAsync(newBlock);
            }
        }
    }

    private static string Slugify(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(), @"[^\w-]", "-")
            .Replace("--", "-")
            .Trim('-');
    }
}
