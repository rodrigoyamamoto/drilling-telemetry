using System.Text.Json.Serialization;

namespace DrillingTelemetry.Contracts;

/// <summary>
/// Identifies how a telemetry reading was acquired.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TelemetryAcquisitionMode>))]
public enum TelemetryAcquisitionMode
{
    /// <summary>
    /// The reading was produced by the live device simulator.
    /// This is the default for messages created before the field
    /// existed and for any payload that omits the value.
    /// </summary>
    RealTime,

    /// <summary>
    /// The reading was produced by importing a historical WITSML log.
    /// </summary>
    HistoricalImport
}
