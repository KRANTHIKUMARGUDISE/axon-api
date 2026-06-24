using Axon.Core.Enums;
using MongoDB.Bson;

namespace Axon.Core.Models;

public class DeliveryStep
{
    public string NodeId { get; set; } = default!;
    public string BlockId { get; set; } = default!;
    public string BlockName { get; set; } = default!;
    public StepStatus Status { get; set; }
    public BsonDocument? ContextSnapshot { get; set; }
    public bool IsContextTruncated { get; set; }
    public string? ContextFileRef { get; set; }
    public Dictionary<string, string>? ContextAvailability { get; set; }
    public Dictionary<string, string>? OutputAvailability { get; set; }
    public AgentOutput? Output { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
