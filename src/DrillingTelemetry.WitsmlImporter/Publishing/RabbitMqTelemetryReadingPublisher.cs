using System.Text.Json;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.WitsmlImporter.Configuration;
using RabbitMQ.Client;

namespace DrillingTelemetry.WitsmlImporter.Publishing;

/// <summary>
/// Publishes telemetry readings to the same durable RabbitMQ queue
/// consumed by the Processor.
/// </summary>
/// <remarks>
/// The publisher uses a single channel and publishes readings
/// sequentially. Publisher confirms are enabled by the RabbitMQ client
/// and awaited through <see cref="IChannel.BasicPublishAsync"/>.
/// </remarks>
internal sealed class RabbitMqTelemetryReadingPublisher
    : ITelemetryReadingPublisher, IAsyncDisposable
{
    private const string DeadLetterExchangeArgument =
        "x-dead-letter-exchange";

    private const string DeadLetterRoutingKeyArgument =
        "x-dead-letter-routing-key";

    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queueName;
    private readonly BasicProperties _properties;

    private RabbitMqTelemetryReadingPublisher(
        IConnection connection,
        IChannel channel,
        string queueName)
    {
        _connection = connection;
        _channel = channel;
        _queueName = queueName;

        _properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true
        };
    }

    /// <summary>
    /// Creates a publisher connected to RabbitMQ and declares the same
    /// durable topology expected by the Processor.
    /// </summary>
    /// <param name="options">
    /// RabbitMQ infrastructure configuration.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel connection and topology setup.
    /// </param>
    /// <returns>
    /// A connected publisher ready to publish readings.
    /// </returns>
    public static async Task<RabbitMqTelemetryReadingPublisher> CreateAsync(
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var connectionFactory = new ConnectionFactory
        {
            HostName = options.HostName
        };

        IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                cancellationToken);

        IChannel channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await DeclareTopologyAsync(channel, options, cancellationToken);

        return new RabbitMqTelemetryReadingPublisher(
            connection,
            channel,
            options.TelemetryReadingsQueueName);
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(reading);

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queueName,
            mandatory: true,
            basicProperties: _properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static async Task DeclareTopologyAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: options.TelemetryDeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: options.TelemetryDeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: options.TelemetryDeadLetterQueueName,
            exchange: options.TelemetryDeadLetterExchangeName,
            routingKey: options.TelemetryReadingsQueueName,
            arguments: null,
            cancellationToken: cancellationToken);

        var queueArguments = new Dictionary<string, object?>
        {
            [DeadLetterExchangeArgument] =
                options.TelemetryDeadLetterExchangeName,
            [DeadLetterRoutingKeyArgument] =
                options.TelemetryReadingsQueueName
        };

        await channel.QueueDeclareAsync(
            queue: options.TelemetryReadingsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);
    }
}
