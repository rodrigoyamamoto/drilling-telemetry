namespace DrillingTelemetry.WitsmlImporter.Configuration;

/// <summary>
/// Contains the RabbitMQ infrastructure configuration used by the
/// WITSML importer to publish readings into the existing telemetry
/// pipeline.
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
    /// Gets or sets the durable telemetry readings queue name consumed
    /// by the Processor.
    /// </summary>
    public string TelemetryReadingsQueueName { get; set; } =
        string.Empty;

    /// <summary>
    /// Gets or sets the dead-letter exchange that receives rejected
    /// telemetry readings.
    /// </summary>
    public string TelemetryDeadLetterExchangeName { get; set; } =
        string.Empty;

    /// <summary>
    /// Gets or sets the dead-letter queue that stores rejected telemetry
    /// readings.
    /// </summary>
    public string TelemetryDeadLetterQueueName { get; set; } =
        string.Empty;
}
