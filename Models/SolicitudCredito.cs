using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CreditosApp.Models;

public class SolicitudCredito
{
    public int Id { get; set; }

    [Required]
    public int ClienteId { get; set; }

    [ForeignKey(nameof(ClienteId))]
    public Cliente? Cliente { get; set; }

    [Required(ErrorMessage = "El monto solicitado es requerido.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Monto Solicitado")]
    public decimal MontoSolicitado { get; set; }

    [Required]
    [Display(Name = "Fecha de Solicitud")]
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    [Required]
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [Display(Name = "Motivo de Rechazo")]
    public string? MotivoRechazo { get; set; }
}
