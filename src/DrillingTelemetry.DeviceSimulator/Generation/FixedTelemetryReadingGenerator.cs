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

    /// <summary>
    /// Initializes a new instance of the
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
    internal FixedTelemetryReadingGenerator(
        TimeProvider timeProvider,
        double pressurePsi,
        double temperatureCelsius)
    {
        _timeProvider = timeProvider;
        _pressurePsi = pressurePsi;
        _temperatureCelsius = temperatureCelsius;
    }

    /// <summary>
    /// Generates a telemetry reading for the specified device.
    /// </summary>
    /// <param name="deviceId">Identifier of the device.</param>
    /// <returns>The generated telemetry reading.</returns>
    public TelemetryReading Generate(string deviceId)
    {
        return new TelemetryReading
        {
            DeviceId = deviceId,
            PressurePsi = _pressurePsi,
            TemperatureCelsius = _temperatureCelsius,
            TimestampUtc = _timeProvider.GetUtcNow(),
        };
    }
}
