namespace DrillingTelemetry.DeviceSimulator.Generation;

/// <summary>
/// Defines how telemetry measurement values are generated.
/// </summary>
internal enum TelemetryGenerationMode
{
    /// <summary>
    /// Generates the same configured measurement values for every reading.
    /// </summary>
    Fixed,

    /// <summary>
    /// Generates measurement values within configured ranges.
    /// </summary>
    Random
}
