using Axon.Core.Interfaces;

namespace Axon.Infrastructure.Services;

public class DeliveryHubStub : IDeliveryHub
{
    public Task SendGateApprovedAsync(string deliveryId, string nodeId) => Task.CompletedTask;
    public Task SendGateRejectedAsync(string deliveryId, string nodeId, string? reason) => Task.CompletedTask;
    public Task SendStatusChangedAsync(string deliveryId, string status) => Task.CompletedTask;
}
