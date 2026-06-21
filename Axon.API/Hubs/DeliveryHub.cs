using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Axon.API.Hubs;

[Authorize]
public class DeliveryHub : Hub
{
    public async Task JoinDelivery(string deliveryId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, deliveryId);
    }
}
