namespace DrillingTelemetry.Processor.Processing;

/// <summary>
/// Identifies the outcome of processing a valid telemetry reading.
/// </summary>
internal enum TelemetryProcessingResult
{
    /// <summary>
    /// The reading was stored and published to real-time clients.
    /// </summary>
    Published,

    /// <summary>
    /// An identical reading was already processed.
    /// </summary>
    Duplicate,

    /// <summary>
    /// The reading was stored but was too old to update the live stream.
    /// </summary>
    LateArrival,

    /// <summary>
    /// The device and sequence identity contained conflicting content.
    /// </summary>
    Conflict
}
