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
    public RabbitMqSimulationSettingsConsumer(
        IChannel channel,
        string queueName,
        SimulationSettingsCommandApplier commandApplier)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(commandApplier);

        _channel = channel;
        _queueName = queueName;
        _commandApplier = commandApplier;
    }

    /// <summary>
    /// Declares the command queue and starts consuming settings commands.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel consumer initialisation.
    /// </param>
    public async Task StartAsync(
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

        await _channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
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

            Console.WriteLine(
                $"Settings revision {command.Revision} applied: " +
                $"{command.DeviceIds.Length} devices, " +
                $"{command.PublishingIntervalMilliseconds} ms interval.");
        }
        catch (Exception exception)
            when (exception is JsonException or ArgumentException)
        {
            Console.WriteLine(
                $"Settings command rejected: {exception.Message}");

            await consumerChannel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);
        }
    }
}
