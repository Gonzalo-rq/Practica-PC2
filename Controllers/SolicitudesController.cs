using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreditosApp.Data;
using CreditosApp.Models;

namespace CreditosApp.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: /Solicitudes o /Solicitudes/MisSolicitudes
    [HttpGet]
    [Route("Solicitudes")]
    [Route("Solicitudes/Index")]
    [Route("Solicitudes/MisSolicitudes")]
    public async Task<IActionResult> Index([FromQuery] FiltroSolicitudesViewModel filtro)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);
        if (cliente == null)
        {
            ViewBag.Mensaje = "No tienes un perfil de cliente registrado actualmente.";
            return View(filtro);
        }

        // Validaciones server-side de filtros
        if (filtro.MontoMin.HasValue && filtro.MontoMin.Value < 0)
        {
            ModelState.AddModelError(nameof(filtro.MontoMin), "El monto mínimo no puede ser negativo.");
        }

        if (filtro.MontoMax.HasValue && filtro.MontoMax.Value < 0)
        {
            ModelState.AddModelError(nameof(filtro.MontoMax), "El monto máximo no puede ser negativo.");
        }

        if (filtro.MontoMin.HasValue && filtro.MontoMax.HasValue && filtro.MontoMin.Value > filtro.MontoMax.Value)
        {
            ModelState.AddModelError(nameof(filtro.MontoMin), "El monto mínimo no puede ser mayor al monto máximo.");
        }

        if (filtro.FechaInicio.HasValue && filtro.FechaFin.HasValue && filtro.FechaInicio.Value > filtro.FechaFin.Value)
        {
            ModelState.AddModelError(nameof(filtro.FechaInicio), "La fecha inicial no puede ser posterior a la fecha final.");
        }

        if (!ModelState.IsValid)
        {
            filtro.Solicitudes = new List<SolicitudCredito>();
            return View(filtro);
        }

        var query = _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .Where(s => s.ClienteId == cliente.Id)
            .AsQueryable();

        if (filtro.Estado.HasValue)
        {
            query = query.Where(s => s.Estado == filtro.Estado.Value);
        }

        if (filtro.MontoMin.HasValue)
        {
            query = query.Where(s => s.MontoSolicitado >= filtro.MontoMin.Value);
        }

        if (filtro.MontoMax.HasValue)
        {
            query = query.Where(s => s.MontoSolicitado <= filtro.MontoMax.Value);
        }

        if (filtro.FechaInicio.HasValue)
        {
            var fInicio = filtro.FechaInicio.Value.Date;
            query = query.Where(s => s.FechaSolicitud >= fInicio);
        }

        if (filtro.FechaFin.HasValue)
        {
            var fFin = filtro.FechaFin.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(s => s.FechaSolicitud <= fFin);
        }

        filtro.Solicitudes = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        return View(filtro);
    }

    // GET: /Solicitudes/Detalle/5
    [HttpGet]
    public async Task<IActionResult> Detalle(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var esAnalista = User.IsInRole("Analista");

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
                .ThenInclude(c => c!.Usuario)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            return NotFound();
        }

        // Solo el propietario o un usuario con rol Analista puede ver el detalle
        if (!esAnalista && solicitud.Cliente?.UsuarioId != userId)
        {
            return Forbid();
        }

        return View(solicitud);
    }
}
