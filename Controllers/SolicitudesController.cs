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

        // Solo el propietario o un analista puede ver el detalle
        if (!esAnalista && solicitud.Cliente?.UsuarioId != userId)
        {
            return Forbid();
        }

        return View(solicitud);
    }

    // GET: /Solicitudes/Crear
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);
        if (cliente == null)
        {
            cliente = new Cliente
            {
                UsuarioId = userId,
                IngresosMensuales = 3500.00m,
                Activo = true
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
        }

        var tienePendiente = await _context.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        var viewModel = new CrearSolicitudViewModel
        {
            IngresosMensualesCliente = cliente.IngresosMensuales,
            ClienteActivo = cliente.Activo,
            TieneSolicitudPendiente = tienePendiente
        };

        if (!cliente.Activo)
        {
            ModelState.AddModelError(string.Empty, "Tu perfil de cliente se encuentra inactivo. No puedes solicitar créditos.");
        }
        else if (tienePendiente)
        {
            ModelState.AddModelError(string.Empty, "Ya cuentas con una solicitud en estado Pendiente. No es posible crear otra hasta que sea evaluada.");
        }

        return View(viewModel);
    }

    // POST: /Solicitudes/Crear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearSolicitudViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);
        if (cliente == null)
        {
            ModelState.AddModelError(string.Empty, "No existe un perfil de cliente asociado a tu usuario.");
            return View(model);
        }

        model.IngresosMensualesCliente = cliente.IngresosMensuales;
        model.ClienteActivo = cliente.Activo;

        // Validaciones server-side de negocio
        if (!cliente.Activo)
        {
            ModelState.AddModelError(string.Empty, "El cliente se encuentra inactivo y no puede registrar solicitudes.");
        }

        if (model.MontoSolicitado <= 0)
        {
            ModelState.AddModelError(nameof(model.MontoSolicitado), "El monto solicitado debe ser mayor a 0.");
        }

        var tienePendiente = await _context.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        model.TieneSolicitudPendiente = tienePendiente;
        if (tienePendiente)
        {
            ModelState.AddModelError(string.Empty, "No se permite más de una solicitud en estado Pendiente por cliente.");
        }

        var montoMaximo = cliente.IngresosMensuales * 10;
        if (model.MontoSolicitado > montoMaximo)
        {
            ModelState.AddModelError(nameof(model.MontoSolicitado), 
                $"El monto solicitado no puede superar 10 veces tus ingresos mensuales ({montoMaximo:C}).");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = model.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };

        _context.SolicitudesCredito.Add(solicitud);
        await _context.SaveChangesAsync();

        ViewBag.MensajeExito = $"¡Solicitud #{solicitud.Id} registrada exitosamente por {solicitud.MontoSolicitado:C}! Su estado inicial es Pendiente.";
        model.TieneSolicitudPendiente = true;
        model.MontoSolicitado = 0;

        return View(model);
    }
}
