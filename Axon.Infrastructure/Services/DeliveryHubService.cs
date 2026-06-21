using Axon.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Axon.Infrastructure.Services;

public class DeliveryHubService<THub> : IDeliveryHub where THub : Hub
{
    private readonly IHubContext<THub> _hub;

    public DeliveryHubService(IHubContext<THub> hub)
    {
        _hub = hub;
    }

    public Task SendGateApprovedAsync(string deliveryId, string nodeId) =>
        _hub.Clients.Group(deliveryId).SendAsync("GateApproved", deliveryId, nodeId);

    public Task SendGateRejectedAsync(string deliveryId, string nodeId, string? reason) =>
        _hub.Clients.Group(deliveryId).SendAsync("GateRejected", deliveryId, nodeId, reason);

    public Task SendStatusChangedAsync(string deliveryId, string status) =>
        _hub.Clients.Group(deliveryId).SendAsync("DeliveryStatusChanged", deliveryId, status);
}
