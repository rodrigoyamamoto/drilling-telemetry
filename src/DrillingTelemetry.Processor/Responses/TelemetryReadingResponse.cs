using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.Processor.Responses;

/// <summary>
/// Represents a persisted telemetry reading returned by the Processor API.
/// </summary>
/// <param name="DeviceId">Identifier of the telemetry device.</param>
/// <param name="AcquisitionSessionId">
/// Acquisition run that owns the sequence number.
/// </param>
/// <param name="SequenceNumber">
/// Sequence number assigned during acquisition.
/// </param>
/// <param name="WellId">Identifier of the well being drilled.</param>
/// <param name="WellName">Name of the well being drilled.</param>
/// <param name="WellboreId">
/// Identifier of the wellbore containing the tool.
/// </param>
/// <param name="WellboreName">
/// Name of the wellbore containing the tool.
/// </param>
/// <param name="MeasuredDepthMetres">
/// Distance travelled along the wellbore, in metres.
/// </param>
/// <param name="DrillingOperation">
/// Operation active when the reading was acquired.
/// </param>
/// <param name="DepthChangeRateMetresPerHour">
/// Signed measured-depth change rate, in metres per hour.
/// </param>
/// <param name="PressurePsi">
/// Pressure in pounds per square inch.
/// </param>
/// <param name="TemperatureCelsius">
/// Temperature in degrees Celsius.
/// </param>
/// <param name="GammaRayApi">
/// Natural gamma ray measurement, in gAPI.
/// </param>
/// <param name="TimestampUtc">UTC acquisition timestamp.</param>
/// <param name="AcquisitionMode">
/// Producer that originated the reading.
/// </param>
public sealed record TelemetryReadingResponse(
    string DeviceId,
    Guid AcquisitionSessionId,
    long SequenceNumber,
    string WellId,
    string WellName,
    string WellboreId,
    string WellboreName,
    double MeasuredDepthMetres,
    DrillingOperation DrillingOperation,
    double DepthChangeRateMetresPerHour,
    double PressurePsi,
    double TemperatureCelsius,
    double GammaRayApi,
    DateTimeOffset TimestampUtc,
    TelemetryAcquisitionMode AcquisitionMode);
