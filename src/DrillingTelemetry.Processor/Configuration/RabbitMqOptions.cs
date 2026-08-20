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
    /// Gets or sets the exchange that receives rejected telemetry readings.
    /// </summary>
    public string TelemetryDeadLetterExchangeName { get; set; } =
        string.Empty;

    /// <summary>
    /// Gets or sets the queue that stores rejected telemetry readings.
    /// </summary>
    public string TelemetryDeadLetterQueueName { get; set; } =
        string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of pending telemetry readings per
    /// processing partition.
    /// </summary>
    public ushort TelemetryPrefetchCount { get; set; }

    /// <summary>
    /// Gets or sets the number of concurrent telemetry processing partitions.
    /// </summary>
    public int TelemetryProcessingPartitionCount { get; set; }
}
