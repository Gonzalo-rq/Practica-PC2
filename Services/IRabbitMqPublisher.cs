using CreditosApp.Models;

namespace CreditosApp.Services;

public interface IRabbitMqPublisher
{
    Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistradaMensaje mensaje);
}
