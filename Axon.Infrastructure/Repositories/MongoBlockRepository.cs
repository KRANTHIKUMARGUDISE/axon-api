using Axon.Core.DTOs.Blocks;
using Axon.Core.Enums;
using Axon.Core.Interfaces;
using Axon.Core.Models;
using Axon.Infrastructure.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Axon.Infrastructure.Repositories;

public class MongoBlockRepository : IBlockRepository
{
    private readonly MongoContext _context;

    public MongoBlockRepository(MongoContext context) => _context = context;

    public async Task<List<BuildingBlock>> GetAllAsync(BlockFilter filter)
    {
        var builder = Builders<BuildingBlock>.Filter;
        var f = builder.Empty;

        if (filter.Role.HasValue)
            f &= builder.Eq(b => b.Role, filter.Role.Value);
        if (filter.SyncStatus.HasValue)
            f &= builder.Eq(b => b.SyncStatus, filter.SyncStatus.Value);
        if (filter.IsActive.HasValue)
            f &= builder.Eq(b => b.IsActive, filter.IsActive.Value);
        if (filter.Tags?.Count > 0)
            f &= builder.AnyIn(b => b.Tags, filter.Tags);
        if (!string.IsNullOrWhiteSpace(filter.Search))
            f &= builder.Or(
                builder.Regex(b => b.Name, new BsonRegularExpression(filter.Search, "i")),
                builder.Regex(b => b.Description, new BsonRegularExpression(filter.Search, "i"))
            );

        return await _context.Blocks.Find(f).ToListAsync();
    }

    public async Task<BuildingBlock?> GetByIdAsync(string id) =>
        await _context.Blocks.Find(ById<BuildingBlock>(id)).FirstOrDefaultAsync();

    public async Task CreateAsync(BuildingBlock block) =>
        await _context.Blocks.InsertOneAsync(block);

    public async Task UpdateAsync(BuildingBlock block)
    {
        block.UpdatedAt = DateTime.UtcNow;
        await _context.Blocks.ReplaceOneAsync(ById<BuildingBlock>(block.Id), block);
    }

    public async Task DeactivateAsync(string id)
    {
        var update = Builders<BuildingBlock>.Update
            .Set(b => b.IsActive, false)
            .Set(b => b.UpdatedAt, DateTime.UtcNow);
        await _context.Blocks.UpdateOneAsync(ById<BuildingBlock>(id), update);
    }

    public async Task UpdateSyncAsync(string id, SyncStatus status)
    {
        var update = Builders<BuildingBlock>.Update
            .Set(b => b.SyncStatus, status)
            .Set(b => b.UpdatedAt, DateTime.UtcNow);
        await _context.Blocks.UpdateOneAsync(ById<BuildingBlock>(id), update);
    }

    private static FilterDefinition<T> ById<T>(string id) =>
        Builders<T>.Filter.Eq("_id", id);
}
