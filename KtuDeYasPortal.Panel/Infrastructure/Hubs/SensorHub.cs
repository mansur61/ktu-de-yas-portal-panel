using Microsoft.AspNetCore.SignalR;

namespace KtuDeYasPortal.Panel.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time sensor status updates.
/// Clients connect to receive live sensor status changes.
/// </summary>
public class SensorHub : Hub
{
    public const string HubPath = "/hubs/sensor";

    public async Task JoinStructureGroup(string structureId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"structure-{structureId}");
    }

    public async Task LeaveStructureGroup(string structureId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"structure-{structureId}");
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
