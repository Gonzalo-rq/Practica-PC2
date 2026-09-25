using System.Text;
using System.Text.Json;
using CreditosApp.Models;
using RabbitMQ.Client;

namespace CreditosApp.Services;

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistradaMensaje mensaje)
    {
        var connectionString = _configuration["RabbitMq:ConnectionString"] 
            ?? Environment.GetEnvironmentVariable("RabbitMq__ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("RabbitMq:ConnectionString no está configurada. Omitiendo publicación.");
            return false;
        }

        var queueName = _configuration["RabbitMq:QueueName"] 
            ?? Environment.GetEnvironmentVariable("RabbitMq__QueueName") 
            ?? "solicitudes.notificaciones";

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString)
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true
            ));

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var json = JsonSerializer.Serialize(mensaje);
            var body = Encoding.UTF8.GetBytes(json);

            var props = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent,
                ContentType = "application/json",
                MessageId = mensaje.MessageId.ToString()
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: props,
                body: body
            );

            _logger.LogInformation("Mensaje SolicitudRegistrada {MessageId} publicado con confirmación en cola {Queue}.", mensaje.MessageId, queueName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falla al publicar mensaje SolicitudRegistrada {MessageId} en RabbitMQ CloudAMQP.", mensaje.MessageId);
            return false;
        }
    }
}
