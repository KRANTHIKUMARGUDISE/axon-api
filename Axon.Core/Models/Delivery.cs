using Axon.Core.Enums;
using MongoDB.Bson;

namespace Axon.Core.Models;

public class Delivery
{
    public string Id { get; set; } = default!;
    public string TicketId { get; set; } = default!;
    public string? TicketTitle { get; set; }
    public string PipelineId { get; set; } = default!;
    public PipelineDefinition PipelineSnapshot { get; set; } = default!;
    public DeliveryStatus Status { get; set; }
    public WorkspaceType WorkspaceType { get; set; }
    public bool StoreFullContext { get; set; } = false;
    public string RepoUrl { get; set; } = default!;
    public string? CurrentNodeId { get; set; }
    public string? WorkspacePath { get; set; }
    public string? Branch { get; set; }
    public List<DeliveryStep> Steps { get; set; } = [];
    public BsonDocument? Inputs { get; set; }
    public string? RetriedFromDeliveryId { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public int JobNumber { get; set; }
    public string OwnerTeam { get; set; } = default!;
    public string CreatedBy { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
