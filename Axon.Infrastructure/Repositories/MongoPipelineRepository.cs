using Axon.Core.Enums;
using Axon.Core.Interfaces;
using Axon.Core.Models;
using Axon.Infrastructure.MongoDB;
using MongoDB.Driver;

namespace Axon.Infrastructure.Repositories;

public class MongoPipelineRepository : IPipelineRepository
{
    private readonly MongoContext _context;
    private readonly IUserRepository _users;

    public MongoPipelineRepository(MongoContext context, IUserRepository users)
    {
        _context = context;
        _users = users;
    }

    public async Task<List<PipelineDefinition>> GetAllAsync(PipelineVisibility? visibility, string? userId)
    {
        var builder = Builders<PipelineDefinition>.Filter;
        var filter = visibility.HasValue
            ? builder.Eq(p => p.Visibility, visibility.Value)
            : builder.Empty;

        filter &= builder.Or(
            builder.Ne(p => p.Visibility, PipelineVisibility.Personal),
            builder.Eq(p => p.CreatedBy, userId)
        );

        return await _context.Pipelines.Find(filter).ToListAsync();
    }

    public async Task<PipelineDefinition?> GetByIdAsync(string id) =>
        await _context.Pipelines.Find(ById<PipelineDefinition>(id)).FirstOrDefaultAsync();

    public async Task CreateAsync(PipelineDefinition pipeline)
    {
        var creator = await _users.GetByIdAsync(pipeline.CreatedBy);
        pipeline.OwnerTeam = creator?.Team ?? string.Empty;
        await _context.Pipelines.InsertOneAsync(pipeline);
    }

    public async Task UpdateAsync(PipelineDefinition pipeline)
    {
        pipeline.Version++;
        pipeline.UpdatedAt = DateTime.UtcNow;
        await _context.Pipelines.ReplaceOneAsync(ById<PipelineDefinition>(pipeline.Id), pipeline);
    }

    public async Task DeleteAsync(string id) =>
        await _context.Pipelines.DeleteOneAsync(ById<PipelineDefinition>(id));

    private static FilterDefinition<T> ById<T>(string id) =>
        Builders<T>.Filter.Eq("_id", id);
}
