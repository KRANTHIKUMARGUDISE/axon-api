namespace Axon.Core.Interfaces;

public interface IDeliveryHub
{
    Task SendGateApprovedAsync(string deliveryId, string nodeId);
    Task SendGateRejectedAsync(string deliveryId, string nodeId, string? reason);
    Task SendStatusChangedAsync(string deliveryId, string status);
}
