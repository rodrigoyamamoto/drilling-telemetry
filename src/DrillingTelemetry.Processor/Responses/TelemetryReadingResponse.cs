namespace DrillingTelemetry.Processor.Responses;

/// <summary>
/// Represents a persisted telemetry reading returned by the Processor API.
/// </summary>
/// <param name="DeviceId">Identifier of the telemetry device.</param>
/// <param name="AcquisitionSessionId">
/// Acquisition session that owns the sequence number.
/// </param>
/// <param name="SequenceNumber">
/// Sequence number assigned during acquisition.
/// </param>
/// <param name="WellId">Identifier of the well being drilled.</param>
/// <param name="WellboreId">
/// Identifier of the wellbore containing the tool.
/// </param>
/// <param name="MeasuredDepthMetres">
/// Distance travelled along the wellbore, in metres.
/// </param>
/// <param name="PressurePsi">
/// Pressure in pounds per square inch.
/// </param>
/// <param name="TemperatureCelsius">
/// Temperature in degrees Celsius.
/// </param>
/// <param name="TimestampUtc">UTC acquisition timestamp.</param>
public sealed record TelemetryReadingResponse(
    string DeviceId,
    Guid AcquisitionSessionId,
    long SequenceNumber,
    string WellId,
    string WellboreId,
    double MeasuredDepthMetres,
    double PressurePsi,
    double TemperatureCelsius,
    DateTimeOffset TimestampUtc);
