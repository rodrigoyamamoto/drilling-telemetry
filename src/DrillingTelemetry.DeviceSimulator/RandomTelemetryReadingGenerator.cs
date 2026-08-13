using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.DeviceSimulator;

/// <summary>
/// Generates telemetry readings using random measurement values.
/// </summary>
internal sealed class RandomTelemetryReadingGenerator : ITelemetryReadingGenerator
{
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;
    private readonly double _minimumPressurePsi;
    private readonly double _maximumPressurePsi;
    private readonly double _minimumTemperatureCelsius;
    private readonly double _maximumTemperatureCelsius;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RandomTelemetryReadingGenerator"/> class.
    /// </summary>
    /// <param name="timeProvider">Provides the current UTC time.</param>
    /// <param name="random">Provides normalized random values.</param>
    /// <param name="minimumPressurePsi">Minimum generated pressure.</param>
    /// <param name="maximumPressurePsi">Maximum generated pressure.</param>
    /// <param name="minimumTemperatureCelsius">
    /// Minimum generated temperature.
    /// </param>
    /// <param name="maximumTemperatureCelsius">
    /// Maximum generated temperature.
    /// </param>
    internal RandomTelemetryReadingGenerator(
        TimeProvider timeProvider,
        Random random,
        double minimumPressurePsi,
        double maximumPressurePsi,
        double minimumTemperatureCelsius,
        double maximumTemperatureCelsius)
    {
        _timeProvider = timeProvider;
        _random = random;
        _minimumPressurePsi = minimumPressurePsi;
        _maximumPressurePsi = maximumPressurePsi;
        _minimumTemperatureCelsius = minimumTemperatureCelsius;
        _maximumTemperatureCelsius = maximumTemperatureCelsius;
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
            PressurePsi = GenerateValue(_minimumPressurePsi, _maximumPressurePsi),
            TemperatureCelsius = GenerateValue(_minimumTemperatureCelsius, _maximumTemperatureCelsius),
            TimestampUtc = _timeProvider.GetUtcNow(),
        };
    }

    /// <summary>
    /// Maps a normalized random value to the specified range.
    /// </summary>
    /// <param name="minimum">Inclusive minimum value.</param>
    /// <param name="maximum">Exclusive maximum value.</param>
    /// <returns>A randomly generated value within the range.</returns>
    private double GenerateValue(double minimum, double maximum)
    {
        return minimum + (_random.NextDouble() * (maximum - minimum));
    }
}
