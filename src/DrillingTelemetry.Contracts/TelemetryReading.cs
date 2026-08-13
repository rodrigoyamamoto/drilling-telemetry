namespace DrillingTelemetry.Contracts;

/// <summary>
/// Represents a telemetry reading exchanged between applications.
/// </summary>
public sealed class TelemetryReading
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pressure in pounds per square inch.
    /// </summary>
    public double PressurePsi { get; set; }

    /// <summary>
    /// Gets or sets the temperature in degrees Celsius.
    /// </summary>
    public double TemperatureCelsius { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the reading.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; }
}
