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
    private readonly string _queueName;
    private readonly SimulationSettingsCommandApplier _commandApplier;
    private readonly ILogger<RabbitMqSimulationSettingsConsumer> _logger;

    /// <summary>
    /// Initialises a RabbitMQ simulation settings consumer.
    /// </summary>
    /// <param name="channel">
    /// RabbitMQ channel used to consume and acknowledge commands.
    /// </param>
    /// <param name="queueName">
    /// Queue from which settings commands are consumed.
    /// </param>
    /// <param name="commandApplier">
    /// Applies received settings commands.
    /// </param>
    /// /// <param name="logger">
    /// Records settings processing information.
    /// </param>
    public RabbitMqSimulationSettingsConsumer(
        IChannel channel,
        string queueName,
        SimulationSettingsCommandApplier commandApplier,
        ILogger<RabbitMqSimulationSettingsConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(commandApplier);
        ArgumentNullException.ThrowIfNull(logger);

        _channel = channel;
        _queueName = queueName;
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
        await _channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
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
            queue: _queueName,
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

            _commandApplier.Apply(command);

            await consumerChannel.BasicAckAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false);

            _logger.LogInformation(
                "Simulation settings revision {Revision} applied: " +
                "{DeviceCount} devices, " +
                "{PublishingIntervalMilliseconds} ms interval",
                command.Revision,
                command.DeviceIds.Length,
                command.PublishingIntervalMilliseconds);
        }
        catch (Exception exception)
            when (exception is JsonException or ArgumentException)
        {
            _logger.LogWarning(
                "Simulation settings command rejected: {Reason}",
                exception.Message);

            await consumerChannel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);
        }
    }
}
