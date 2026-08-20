using System.Text.Json;
using DrillingTelemetry.Contracts.Commands;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DrillingTelemetry.DeviceSimulator.RuntimeSettings;

/// <summary>
/// Receives simulation settings commands from RabbitMQ.
/// </summary>
internal sealed class RabbitMqSimulationSettingsConsumer
{
    private readonly IChannel _channel;
    private readonly string _exchangeName;
    private readonly SimulationSettingsCommandApplier _commandApplier;
    private readonly ILogger<RabbitMqSimulationSettingsConsumer> _logger;

    /// <summary>
    /// Initialises a RabbitMQ simulation settings consumer.
    /// </summary>
    /// <param name="channel">
    /// RabbitMQ channel used to consume and acknowledge commands.
    /// </param>
    /// <param name="exchangeName">
    /// Exchange from which settings commands are received.
    /// </param>
    /// <param name="commandApplier">
    /// Applies received settings commands.
    /// </param>
    /// <param name="logger">
    /// Records settings processing information.
    /// </param>
    public RabbitMqSimulationSettingsConsumer(
        IChannel channel,
        string exchangeName,
        SimulationSettingsCommandApplier commandApplier,
        ILogger<RabbitMqSimulationSettingsConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentNullException.ThrowIfNull(commandApplier);
        ArgumentNullException.ThrowIfNull(logger);

        _channel = channel;
        _exchangeName = exchangeName;
        _commandApplier = commandApplier;
        _logger = logger;
    }

    /// <summary>
    /// Declares the command queue and starts consuming settings commands.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel consumer initialisation.
    /// </param>
    /// <returns>
    /// Consumer tag assigned by RabbitMQ.
    /// </returns>
    public async Task<string> StartAsync(
        CancellationToken cancellationToken)
    {
        await _channel.ExchangeDeclareAsync(
            exchange: _exchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        QueueDeclareOk queue = await _channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            queue: queue.QueueName,
            exchange: _exchangeName,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += HandleReceivedAsync;

        string consumerTag = await _channel.BasicConsumeAsync(
            queue: queue.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        return consumerTag;
    }

    /// <summary>
    /// Deserialises, applies and acknowledges a settings command.
    /// </summary>
    /// <param name="sender">
    /// RabbitMQ consumer that received the command.
    /// </param>
    /// <param name="eventArgs">
    /// RabbitMQ delivery information.
    /// </param>
    private async Task HandleReceivedAsync(
        object sender,
        BasicDeliverEventArgs eventArgs)
    {
        var eventConsumer = (AsyncEventingBasicConsumer)sender;

        IChannel consumerChannel = eventConsumer.Channel;

        try
        {
            UpdateSimulationSettingsCommand? command =
                JsonSerializer.Deserialize<UpdateSimulationSettingsCommand>(
                    eventArgs.Body.Span);

            if (command is null)
            {
                throw new JsonException(
                    "The settings command is empty.");
            }

            bool applied = _commandApplier.TryApply(command);

            await consumerChannel.BasicAckAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false);

            if (!applied)
            {
                _logger.LogDebug(
                    "Obsolete simulation settings revision " +
                    "{Revision} ignored",
                    command.Revision);

                return;
            }

            _logger.LogInformation(
                "Simulation settings revision {Revision} applied: " +
                "{DeviceCount} devices, " +
                "{PublishingIntervalMilliseconds} ms interval, " +
                "{DrillingOperation} at " +
                "{DepthChangeRateMetresPerHour} m/h",
                command.Revision,
                command.DeviceIds.Length,
                command.PublishingIntervalMilliseconds,
                command.DrillingOperation,
                command.DepthChangeRateMetresPerHour);
        }
        catch (Exception exception)
            when (exception is JsonException or ArgumentException)
        {
            _logger.LogWarning(
                "Simulation settings command rejected: {Reason}",
                exception.Message);

            await RejectDeliveryAsync(
                consumerChannel,
                eventArgs.DeliveryTag);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected failure processing simulation settings " +
                "delivery {DeliveryTag}",
                eventArgs.DeliveryTag);

            await RejectDeliveryAsync(
                consumerChannel,
                eventArgs.DeliveryTag);
        }
    }

    /// <summary>
    /// Rejects a delivery without requeueing it and records channel failures.
    /// </summary>
    /// <param name="channel">
    /// Channel on which the delivery was received.
    /// </param>
    /// <param name="deliveryTag">
    /// RabbitMQ delivery identifier.
    /// </param>
    private async Task RejectDeliveryAsync(
        IChannel channel,
        ulong deliveryTag)
    {
        try
        {
            await channel.BasicNackAsync(
                deliveryTag: deliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to reject simulation settings delivery " +
                "{DeliveryTag}",
                deliveryTag);
        }
    }
}
