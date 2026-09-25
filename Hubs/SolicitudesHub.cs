using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CreditosApp.Hubs;

[Authorize]
public class SolicitudesHub : Hub
{
    private readonly ILogger<SolicitudesHub> _logger;

    public SolicitudesHub(ILogger<SolicitudesHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Usuario autenticado {UserId} conectado a SolicitudesHub mediante WebSocket.", Context.UserIdentifier);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Usuario {UserId} desconectado de SolicitudesHub.", Context.UserIdentifier);
        await base.OnDisconnectedAsync(exception);
    }
}
