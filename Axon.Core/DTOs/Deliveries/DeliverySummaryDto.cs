using Axon.Core.Enums;

namespace Axon.Core.DTOs.Deliveries;

public class DeliverySummaryDto
{
    public string Id { get; set; } = default!;
    public string TicketId { get; set; } = default!;
    public string? TicketTitle { get; set; }
    public string PipelineId { get; set; } = default!;
    public string PipelineName { get; set; } = default!;
    public DeliveryStatus Status { get; set; }
    public WorkspaceType WorkspaceType { get; set; }
    public string RepoUrl { get; set; } = default!;
    public int StepCount { get; set; }
    public int CompletedSteps { get; set; }
    public int JobNumber { get; set; }
    public string OwnerTeam { get; set; } = default!;
    public string CreatedBy { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
