using MongoDB.Bson;

namespace Axon.Core.Models;

public class AgentOutput
{
    public string Status { get; set; } = default!;
    public float? Confidence { get; set; }
    public string Summary { get; set; } = default!;
    public BsonDocument Result { get; set; } = new();
    public string? HumanGateReason { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public bool? Recoverable { get; set; }
    // Gate resolution metadata — kept as first-class fields rather than inside
    // Result, since Result is excluded from the main delivery-detail response
    // (lazy-loaded only); these are small/structured and need to always be visible.
    public string? ApprovedBy { get; set; }
    public string? ApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectedAt { get; set; }
    public string? ReviewComment { get; set; }
    public bool IsTruncated { get; set; }
    public string? OutputFileRef { get; set; }
}
