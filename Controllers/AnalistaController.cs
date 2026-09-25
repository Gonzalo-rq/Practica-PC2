using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreditosApp.Data;
using CreditosApp.Models;
using CreditosApp.Services;

namespace CreditosApp.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICreditCacheService _cacheService;
    private readonly ILogger<AnalistaController> _logger;

    public AnalistaController(
        ApplicationDbContext context,
        ICreditCacheService cacheService,
        ILogger<AnalistaController> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    // GET: /Analista
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var solicitudesPendientes = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
                .ThenInclude(c => c!.Usuario)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        return View(solicitudesPendientes);
    }

    // POST: /Analista/Aprobar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            TempData["Error"] = $"Solicitud #{id} no encontrada.";
            return RedirectToAction(nameof(Index));
        }

        // Validación: no procesar solicitudes ya resueltas
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue evaluada previamente con estado {solicitud.Estado}.";
            return RedirectToAction(nameof(Index));
        }

        // Validación: No aprobar si el monto excede 5 veces los ingresos
        var limiteAprobacion = solicitud.Cliente!.IngresosMensuales * 5;
        if (solicitud.MontoSolicitado > limiteAprobacion)
        {
            TempData["Error"] = $"Regla de Riesgo: No se puede aprobar la solicitud #{id}. El monto ({solicitud.MontoSolicitado:C}) supera el límite de 5 veces los ingresos mensuales ({limiteAprobacion:C}).";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        solicitud.MotivoRechazo = null;
        await _context.SaveChangesAsync();

        // Invalidar caché de Redis del cliente propietario
        if (!string.IsNullOrEmpty(solicitud.Cliente?.UsuarioId))
        {
            await _cacheService.InvalidarSolicitudesUsuarioAsync(solicitud.Cliente.UsuarioId);
        }

        _logger.LogInformation("Solicitud #{Id} aprobada por el analista {Analista}.", id, User.Identity?.Name);
        TempData["Exito"] = $"Solicitud #{id} aprobada exitosamente.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Analista/Rechazar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            TempData["Error"] = $"Solicitud #{id} no encontrada.";
            return RedirectToAction(nameof(Index));
        }

        // Validación: no procesar solicitudes ya resueltas
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue evaluada previamente con estado {solicitud.Estado}.";
            return RedirectToAction(nameof(Index));
        }

        // Validación: motivo obligatorio
        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            TempData["Error"] = "El motivo de rechazo es obligatorio para rechazar una solicitud.";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo.Trim();
        await _context.SaveChangesAsync();

        // Invalidar caché de Redis del cliente propietario
        if (!string.IsNullOrEmpty(solicitud.Cliente?.UsuarioId))
        {
            await _cacheService.InvalidarSolicitudesUsuarioAsync(solicitud.Cliente.UsuarioId);
        }

        _logger.LogInformation("Solicitud #{Id} rechazada por el analista {Analista}. Motivo: {Motivo}", id, User.Identity?.Name, motivoRechazo);
        TempData["Exito"] = $"Solicitud #{id} rechazada correctamente.";

        return RedirectToAction(nameof(Index));
    }
}
