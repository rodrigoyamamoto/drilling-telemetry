namespace DrillingTelemetry.Processor.Configuration;

/// <summary>
/// Contains the RabbitMQ infrastructure configuration.
/// </summary>
internal sealed class RabbitMqOptions
{
    /// <summary>
    /// Configuration section containing the RabbitMQ settings.
    /// </summary>
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// Gets or sets the RabbitMQ server host name.
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the telemetry readings queue name.
    /// </summary>
    public string TelemetryReadingsQueueName { get; set; } =
        string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of unacknowledged telemetry readings per consumer.
    /// </summary>
    public ushort TelemetryPrefetchCount { get; set; }

    /// <summary>
    /// Gets or sets the number of concurrent telemetry consumers.
    /// </summary>
    public int TelemetryConsumerCount { get; set; }
}
