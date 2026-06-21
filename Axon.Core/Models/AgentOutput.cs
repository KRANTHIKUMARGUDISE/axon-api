using MongoDB.Bson;

namespace Axon.Core.Models;

public class AgentOutput
{
    public string Status { get; set; } = default!;
    public float Confidence { get; set; }
    public string Summary { get; set; } = default!;
    public BsonDocument Result { get; set; } = new();
    public string? HumanGateReason { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsTruncated { get; set; }
    public string? OutputFileRef { get; set; }
}
