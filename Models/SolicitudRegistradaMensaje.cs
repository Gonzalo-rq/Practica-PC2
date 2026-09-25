namespace CreditosApp.Models;

public class SolicitudRegistradaMensaje
{
    public Guid MessageId { get; set; }
    public int SolicitudId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime FechaEventoUtc { get; set; }
}
