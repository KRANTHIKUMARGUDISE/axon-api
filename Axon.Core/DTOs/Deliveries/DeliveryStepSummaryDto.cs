using Axon.Core.Enums;

namespace Axon.Core.DTOs.Deliveries;

// Lightweight counterpart to DeliveryStep — everything except the two fields that
// can be arbitrarily large (ContextSnapshot, Output.Result). Used on the main
// delivery-detail response; full values are fetched lazily via the dedicated
// /steps/{stepId}/context and /steps/{stepId}/output endpoints.
public class DeliveryStepSummaryDto
{
    public string NodeId { get; set; } = default!;
    public string BlockId { get; set; } = default!;
    public string BlockName { get; set; } = default!;
    public StepStatus Status { get; set; }
    public bool IsContextTruncated { get; set; }
    public string? ContextFileRef { get; set; }
    public Dictionary<string, string>? ContextAvailability { get; set; }
    public Dictionary<string, string>? OutputAvailability { get; set; }
    public AgentOutputSummaryDto? Output { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// AgentOutput without Result.
public class AgentOutputSummaryDto
{
    public string Status { get; set; } = default!;
    public float? Confidence { get; set; }
    public string Summary { get; set; } = default!;
    public string? HumanGateReason { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public bool? Recoverable { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectedAt { get; set; }
    public string? ReviewComment { get; set; }
    public bool IsTruncated { get; set; }
    public string? OutputFileRef { get; set; }
    public List<string>? ToolsUsed { get; set; }
    public List<string>? ToolsDenied { get; set; }
    public bool? TimedOut { get; set; }
}
