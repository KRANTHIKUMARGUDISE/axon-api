using Axon.Core.Enums;

namespace Axon.Core.DTOs.Deliveries;

public class UpdateStatusRequest
{
    public DeliveryStatus Status { get; set; }
    public string? CurrentNodeId { get; set; }
    public string? WorkspacePath { get; set; }
    public string? TicketTitle { get; set; }
    public string? Branch { get; set; }
}
