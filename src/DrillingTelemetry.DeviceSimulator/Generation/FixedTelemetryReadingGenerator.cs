using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.DeviceSimulator.Generation;

/// <summary>
/// Generates telemetry readings using fixed measurement values.
/// </summary>
internal sealed class FixedTelemetryReadingGenerator : ITelemetryReadingGenerator
{
    private readonly TimeProvider _timeProvider;
    private readonly double _pressurePsi;
    private readonly double _temperatureCelsius;
    private readonly double _gammaRayApi;

    /// <summary>
    /// Initialises a new instance of the
    /// <see cref="FixedTelemetryReadingGenerator"/> class.
    /// </summary>
    /// <param name="timeProvider">
    /// Provides the current UTC time.
    /// </param>
    /// <param name="pressurePsi">
    /// Pressure assigned to every generated reading.
    /// </param>
    /// <param name="temperatureCelsius">
    /// Temperature assigned to every generated reading.
    /// </param>
    /// <param name="gammaRayApi">
    /// Natural gamma ray, in gAPI, assigned to every generated reading.
    /// </param>
    internal FixedTelemetryReadingGenerator(
        TimeProvider timeProvider,
        double pressurePsi,
        double temperatureCelsius,
        double gammaRayApi)
    {
        _timeProvider = timeProvider;
        _pressurePsi = pressurePsi;
        _temperatureCelsius = temperatureCelsius;
        _gammaRayApi = gammaRayApi;
    }

    /// <summary>
    /// Generates a telemetry reading for the specified device at the given
    /// measured depth.
    /// </summary>
    /// <param name="deviceId">Identifier of the device.</param>
    /// <param name="measuredDepthMetres">
    /// Measured depth, in metres, assigned to the generated reading.
    /// </param>
    /// <returns>The generated telemetry reading.</returns>
    public TelemetryReading Generate(
        string deviceId,
        double measuredDepthMetres)
    {
        return new TelemetryReading
        {
            DeviceId = deviceId,
            MeasuredDepthMetres = measuredDepthMetres,
            PressurePsi = _pressurePsi,
            TemperatureCelsius = _temperatureCelsius,
            GammaRayApi = _gammaRayApi,
            TimestampUtc = _timeProvider.GetUtcNow(),
        };
    }
}
