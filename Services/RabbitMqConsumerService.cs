using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CreditosApp.Data;
using CreditosApp.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CreditosApp.Services;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    public RabbitMqConsumerService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<RabbitMqConsumerService> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Verificar si el consumidor está habilitado mediante configuración o variable de entorno
        var consumerEnabledStr = _configuration["RabbitMq:ConsumerEnabled"] 
            ?? Environment.GetEnvironmentVariable("RabbitMq__ConsumerEnabled") 
            ?? "true";

        if (!bool.TryParse(consumerEnabledStr, out var consumerEnabled) || !consumerEnabled)
        {
            _logger.LogWarning("RabbitMQ Consumer deshabilitado (RabbitMq:ConsumerEnabled = false). El servicio no consumirá mensajes de la cola.");
            return;
        }

        var connectionString = _configuration["RabbitMq:ConnectionString"] 
            ?? Environment.GetEnvironmentVariable("RabbitMq__ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("RabbitMq:ConnectionString no está configurada. El consumidor RabbitMQ no se iniciará.");
            return;
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

            var connection = await factory.CreateConnectionAsync(stoppingToken);
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            // Prefetch count 1 para control estricto de concurrencia y confirmaciones manuales
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var deliveryTag = ea.DeliveryTag;
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    SolicitudRegistradaMensaje? mensaje;
                    try
                    {
                        mensaje = JsonSerializer.Deserialize<SolicitudRegistradaMensaje>(json);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Mensaje inválido recibido en cola {Queue}. Rechazando sin reencolar.", queueName);
                        await channel.BasicRejectAsync(deliveryTag: deliveryTag, requeue: false);
                        return;
                    }

                    if (mensaje == null || mensaje.MessageId == Guid.Empty || mensaje.SolicitudId <= 0)
                    {
                        _logger.LogError("Contenido de mensaje incompleto o corrupto. Rechazando sin reencolar.");
                        await channel.BasicRejectAsync(deliveryTag: deliveryTag, requeue: false);
                        return;
                    }

                    // Procesar dentro de un scope de DI para ApplicationDbContext
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // 1. Idempotencia: Verificar si el MessageId ya fue procesado
                    var yaExiste = await dbContext.Notificaciones.AnyAsync(n => n.MessageId == mensaje.MessageId);
                    if (yaExiste)
                    {
                        _logger.LogInformation("Mensaje {MessageId} ya fue procesado previamente. Confirmando con ACK manual sin duplicar.", mensaje.MessageId);
                        await channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false);
                        return;
                    }

                    // 2. Persistir notificación en base de datos SQLite
                    var notificacion = new Notificacion
                    {
                        MessageId = mensaje.MessageId,
                        SolicitudId = mensaje.SolicitudId,
                        UsuarioId = mensaje.UsuarioId,
                        Texto = "Recibimos tu solicitud de crédito y está pendiente de evaluación.",
                        FechaProcesamientoUtc = DateTime.UtcNow
                    };

                    dbContext.Notificaciones.Add(notificacion);
                    await dbContext.SaveChangesAsync();

                    // 3. Confirmación con ACK manual ÚNICAMENTE tras persistir exitosamente en BD
                    await channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false);
                    _logger.LogInformation("Notificación persistida y mensaje {MessageId} confirmado con ACK en RabbitMQ.", mensaje.MessageId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar mensaje en cola {Queue}. No se envía ACK para permitir análisis.", queueName);
                    // No confirmar como exitoso ante errores inesperados
                }
            };

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation("Consumidor RabbitMQ iniciado y escuchando activamente en la cola {Queue}.", queueName);

            // Mantener el servicio activo hasta cancelación
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumidor RabbitMQ detenido por cancelación.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el ciclo de vida del consumidor RabbitMQ.");
        }
    }
}
