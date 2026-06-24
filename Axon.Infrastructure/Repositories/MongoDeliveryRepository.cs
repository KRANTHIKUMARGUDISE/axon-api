using Axon.Core.Enums;
using Axon.Core.Interfaces;
using Axon.Core.Models;
using Axon.Infrastructure.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Axon.Infrastructure.Repositories;

public class MongoDeliveryRepository : IDeliveryRepository
{
    private readonly MongoContext _context;

    public MongoDeliveryRepository(MongoContext context) => _context = context;

    public async Task<List<Delivery>> GetAllByUserAsync(string userId, DeliveryStatus? status)
    {
        var filter = Builders<Delivery>.Filter.Eq(d => d.CreatedBy, userId);
        if (status.HasValue)
            filter &= Builders<Delivery>.Filter.Eq(d => d.Status, status.Value);
        return await _context.Deliveries.Find(filter)
            .SortByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<Delivery?> GetByIdAsync(string id, string userId)
    {
        var filter = ById<Delivery>(id) & Builders<Delivery>.Filter.Eq(d => d.CreatedBy, userId);
        return await _context.Deliveries.Find(filter).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(Delivery delivery)
    {
        delivery.OwnerTeam = delivery.PipelineSnapshot?.OwnerTeam ?? string.Empty;
        await _context.Deliveries.InsertOneAsync(delivery);
    }

    public async Task UpdateAsync(string id, string? repoUrl, WorkspaceType? workspaceType, string? workspacePath)
    {
        var update = Builders<Delivery>.Update.Set(d => d.UpdatedAt, DateTime.UtcNow);
        if (repoUrl != null) update = update.Set(d => d.RepoUrl, repoUrl);
        if (workspaceType.HasValue) update = update.Set(d => d.WorkspaceType, workspaceType.Value);
        if (workspacePath != null) update = update.Set(d => d.WorkspacePath, workspacePath);
        await _context.Deliveries.UpdateOneAsync(ById<Delivery>(id), update);
    }

    public async Task UpdateStatusAsync(string id, DeliveryStatus status, string? currentNodeId, string? workspacePath, string? ticketTitle, DateTime? startedAt = null, string? branch = null)
    {
        var update = Builders<Delivery>.Update
            .Set(d => d.Status, status)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);

        if (currentNodeId != null)
            update = update.Set(d => d.CurrentNodeId, currentNodeId);

        if (workspacePath != null)
            update = update.Set(d => d.WorkspacePath, workspacePath);

        if (ticketTitle != null)
            update = update.Set(d => d.TicketTitle, ticketTitle);

        if (startedAt.HasValue)
            update = update.Set(d => d.StartedAt, startedAt.Value);

        if (branch != null)
            update = update.Set(d => d.Branch, branch);

        if (status is DeliveryStatus.Completed or DeliveryStatus.Failed or DeliveryStatus.Cancelled)
            update = update.Set(d => d.CompletedAt, DateTime.UtcNow);

        await _context.Deliveries.UpdateOneAsync(ById<Delivery>(id), update);
    }

    public async Task AppendStepAsync(string id, DeliveryStep step) =>
        await _context.Deliveries.UpdateOneAsync(
            ById<Delivery>(id),
            Builders<Delivery>.Update
                .Push(d => d.Steps, step)
                .Set(d => d.UpdatedAt, DateTime.UtcNow));

    public async Task UpdateStepAsync(string id, string nodeId, DeliveryStep updatedStep)
    {
        var delivery = await _context.Deliveries.Find(ById<Delivery>(id)).FirstOrDefaultAsync();
        if (delivery == null) return;

        var idx = delivery.Steps.FindIndex(s => s.NodeId == nodeId);
        if (idx < 0) return;

        delivery.Steps[idx] = updatedStep;
        await _context.Deliveries.UpdateOneAsync(
            ById<Delivery>(id),
            Builders<Delivery>.Update
                .Set(d => d.Steps, delivery.Steps)
                .Set(d => d.UpdatedAt, DateTime.UtcNow));
    }

    private static FilterDefinition<T> ById<T>(string id) =>
        Builders<T>.Filter.Eq("_id", id);

    public async Task UpdateGateStatusAsync(string id, string nodeId, bool approved, string? reason, string approvedByUserId, string? approvedByName)
    {
        var delivery = await _context.Deliveries.Find(ById<Delivery>(id)).FirstOrDefaultAsync();
        if (delivery == null) return;

        var idx = delivery.Steps.FindIndex(s => s.NodeId == nodeId);
        if (idx >= 0 && delivery.Steps[idx].Output != null)
        {
            var output = delivery.Steps[idx].Output!;
            var resolvedAt = DateTime.UtcNow;
            if (approved)
            {
                output.ApprovedBy = approvedByName ?? approvedByUserId;
                output.ApprovedAt = resolvedAt.ToString("o");
                if (!string.IsNullOrWhiteSpace(reason)) output.ReviewComment = reason;
                delivery.Steps[idx].Status = StepStatus.Completed;
            }
            else
            {
                output.ErrorMessage = reason;
                output.RejectedBy = approvedByName ?? approvedByUserId;
                output.RejectedAt = resolvedAt.ToString("o");
                delivery.Steps[idx].Status = StepStatus.Failed;
            }
            // This gate step was left CompletedAt=null at append time (AwaitingApproval
            // isn't finished yet) — now that it's resolved, it actually is.
            delivery.Steps[idx].CompletedAt = resolvedAt;

            var update = Builders<Delivery>.Update
                .Set(d => d.Steps, delivery.Steps)
                .Set(d => d.UpdatedAt, DateTime.UtcNow);
            if (!approved)
                update = update.Set(d => d.Status, DeliveryStatus.Failed);

            await _context.Deliveries.UpdateOneAsync(ById<Delivery>(id), update);
        }
    }

    public async Task<int> GetNextJobNumberAsync()
    {
        var counters = _context.GetRawCollection<BsonDocument>("counters");
        var filter = Builders<BsonDocument>.Filter.Eq("_id", "delivery_job_number");
        var update = Builders<BsonDocument>.Update.Inc("seq", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        };
        var result = await counters.FindOneAndUpdateAsync(filter, update, options);
        return result["seq"].AsInt32;
    }
}
