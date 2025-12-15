using AegisDrive.Api.Contracts.RealTime;
using AegisDrive.Api.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AegisDrive.Api.Hubs;



[Authorize] 
// <--- Requires valid JWT to connect
public class FleetHub : Hub<IFleetClient>
{
    private readonly ILogger<FleetHub> _logger;

    public FleetHub(ILogger<FleetHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // 1. Get User Info from the JWT Claims
        var user = Context.User;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value.ToLower();
        var companyId = user?.FindFirst(AuthConstants.Claims.CompanyId)?.Value;
        var role = user?.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null)
        {
            await base.OnConnectedAsync();
            return;
        }

        // 2. Assign to Groups based on Role
        if (role == AuthConstants.Roles.Manager && !string.IsNullOrEmpty(companyId))
        {
            // Managers listen to the WHOLE Company
            var groupName = $"Company_{companyId}".ToLower();
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"Manager {userId} joined group {groupName}");
        }
        else
        {
            // Individuals (or Drivers) listen only to their OWN Personal Channel
            var groupName = $"User_{userId}".ToLower();
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"User {userId} joined group {groupName}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value.ToLower();
        _logger.LogInformation($"User {userId} disconnected.");

        await base.OnDisconnectedAsync(exception);
    }
}