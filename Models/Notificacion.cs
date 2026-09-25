using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace CreditosApp.Models;

public class Notificacion
{
    public int Id { get; set; }

    [Required]
    public Guid MessageId { get; set; }

    [Required]
    public int SolicitudId { get; set; }

    [ForeignKey(nameof(SolicitudId))]
    public SolicitudCredito? Solicitud { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [ForeignKey(nameof(UsuarioId))]
    public IdentityUser? Usuario { get; set; }

    [Required]
    [StringLength(500)]
    public string Texto { get; set; } = string.Empty;

    [Required]
    public DateTime FechaProcesamientoUtc { get; set; } = DateTime.UtcNow;
}
