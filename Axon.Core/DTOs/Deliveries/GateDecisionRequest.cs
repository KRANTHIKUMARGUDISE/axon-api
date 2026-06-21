namespace Axon.Core.DTOs.Deliveries;

public class GateDecisionRequest
{
    public bool Approved { get; set; }
    public string? Reason { get; set; }
}
