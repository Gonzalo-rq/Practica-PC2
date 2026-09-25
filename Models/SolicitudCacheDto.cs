namespace CreditosApp.Models;

public class SolicitudCacheDto
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public decimal MontoSolicitado { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoSolicitud Estado { get; set; }
    public string? MotivoRechazo { get; set; }
}
