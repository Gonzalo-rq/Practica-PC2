using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models;

public class FiltroSolicitudesViewModel
{
    public EstadoSolicitud? Estado { get; set; }

    [Display(Name = "Monto Mínimo")]
    public decimal? MontoMin { get; set; }

    [Display(Name = "Monto Máximo")]
    public decimal? MontoMax { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha Desde")]
    public DateTime? FechaInicio { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha Hasta")]
    public DateTime? FechaFin { get; set; }

    public List<SolicitudCredito> Solicitudes { get; set; } = new();
}
