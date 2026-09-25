using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models;

public class CrearSolicitudViewModel
{
    [Required(ErrorMessage = "El monto solicitado es requerido.")]
    [Range(0.01, 100000000, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Display(Name = "Monto Solicitado")]
    public decimal MontoSolicitado { get; set; }

    // Información del cliente para apoyo visual y contexto en el formulario
    public decimal IngresosMensualesCliente { get; set; }
    public decimal MontoMaximoPermitido => IngresosMensualesCliente * 10;
    public bool ClienteActivo { get; set; } = true;
    public bool TieneSolicitudPendiente { get; set; } = false;
}
