using Axon.Core.Enums;
using MongoDB.Bson;

namespace Axon.Core.DTOs.Deliveries;

public class CreateDeliveryRequest
{
    public string TicketId { get; set; } = default!;
    public string? TicketTitle { get; set; }
    public string PipelineId { get; set; } = default!;
    public string RepoUrl { get; set; } = default!;
    public WorkspaceType WorkspaceType { get; set; }
    public BsonDocument? Inputs { get; set; }
    public string? RetriedFromDeliveryId { get; set; }
}
