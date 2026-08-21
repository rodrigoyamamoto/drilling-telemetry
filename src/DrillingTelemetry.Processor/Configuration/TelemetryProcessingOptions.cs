namespace DrillingTelemetry.Processor.Configuration;

/// <summary>
/// Configures rules applied while processing accepted telemetry readings.
/// </summary>
internal sealed class TelemetryProcessingOptions
{
    /// <summary>Configuration section containing processing options.</summary>
    public const string SectionName = "TelemetryProcessing";

    /// <summary>
    /// Gets or sets the activity window, in seconds, within which different
    /// acquisition sessions for the same device are considered concurrent.
    /// </summary>
    public int ConcurrentAcquisitionSessionActivityWindowSeconds { get; set; }
}
