using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using CreditosApp.Models;

namespace CreditosApp.Services;

public class CreditCacheService : ICreditCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CreditCacheService> _logger;
    private const string KeyPrefix = "solicitudes_usuario_";

    public CreditCacheService(IDistributedCache cache, ILogger<CreditCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<SolicitudCredito>?> ObtenerSolicitudesUsuarioAsync(string userId)
    {
        var key = $"{KeyPrefix}{userId}";
        try
        {
            var cachedJson = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(cachedJson))
            {
                return null;
            }

            var dtos = JsonSerializer.Deserialize<List<SolicitudCacheDto>>(cachedJson);
            if (dtos == null) return null;

            return dtos.Select(d => new SolicitudCredito
            {
                Id = d.Id,
                ClienteId = d.ClienteId,
                MontoSolicitado = d.MontoSolicitado,
                FechaSolicitud = d.FechaSolicitud,
                Estado = d.Estado,
                MotivoRechazo = d.MotivoRechazo
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al leer de Redis para la clave {Key}. Continuando sin caché.", key);
            return null;
        }
    }

    public async Task GuardarSolicitudesUsuarioAsync(string userId, List<SolicitudCredito> solicitudes, TimeSpan expiracion)
    {
        var key = $"{KeyPrefix}{userId}";
        try
        {
            var dtos = solicitudes.Select(s => new SolicitudCacheDto
            {
                Id = s.Id,
                ClienteId = s.ClienteId,
                MontoSolicitado = s.MontoSolicitado,
                FechaSolicitud = s.FechaSolicitud,
                Estado = s.Estado,
                MotivoRechazo = s.MotivoRechazo
            }).ToList();

            var json = JsonSerializer.Serialize(dtos);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiracion
            };

            await _cache.SetStringAsync(key, json, options);
            _logger.LogInformation("Solicitudes cacheadas en Redis para usuario {UserId} por {Seconds}s.", userId, expiracion.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al escribir en Redis para la clave {Key}.", key);
        }
    }

    public async Task InvalidarSolicitudesUsuarioAsync(string userId)
    {
        var key = $"{KeyPrefix}{userId}";
        try
        {
            await _cache.RemoveAsync(key);
            _logger.LogInformation("Caché invalidada en Redis para usuario {UserId}.", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invalidar caché en Redis para la clave {Key}.", key);
        }
    }
}
