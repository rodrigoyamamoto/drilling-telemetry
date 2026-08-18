namespace DrillingTelemetry.Processor.Persistence;

/// <summary>
/// Identifies the result of storing a telemetry reading by its natural key.
/// </summary>
internal enum TelemetryReadingStoreResult
{
    /// <summary>
    /// The reading was stored for the first time.
    /// </summary>
    Stored,

    /// <summary>
    /// An identical reading was already stored.
    /// </summary>
    Duplicate,

    /// <summary>
    /// The identity was already used by a reading with different content.
    /// </summary>
    Conflict
}
