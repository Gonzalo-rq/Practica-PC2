using CreditosApp.Models;

namespace CreditosApp.Services;

public interface ICreditCacheService
{
    Task<List<SolicitudCredito>?> ObtenerSolicitudesUsuarioAsync(string userId);
    Task GuardarSolicitudesUsuarioAsync(string userId, List<SolicitudCredito> solicitudes, TimeSpan expiracion);
    Task InvalidarSolicitudesUsuarioAsync(string userId);
}
