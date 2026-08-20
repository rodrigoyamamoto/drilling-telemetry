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
    /// Gets or sets the acquisition session that owns the sequence number.
    /// </summary>
    public Guid AcquisitionSessionId { get; set; }

    /// <summary>
    /// Gets or sets the sequence number assigned by the device simulator.
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the well being drilled.
    /// </summary>
    public string WellId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the wellbore containing the tool.
    /// </summary>
    public string WellboreId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the distance travelled along the wellbore, in metres.
    /// </summary>
    public double MeasuredDepthMetres { get; set; }

    /// <summary>
    /// Gets or sets the operation that governed measured-depth movement when
    /// the reading was acquired.
    /// </summary>
    public DrillingOperation DrillingOperation { get; set; }

    /// <summary>
    /// Gets or sets the signed measured-depth change rate, in metres per hour.
    /// </summary>
    public double DepthChangeRateMetresPerHour { get; set; }

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
