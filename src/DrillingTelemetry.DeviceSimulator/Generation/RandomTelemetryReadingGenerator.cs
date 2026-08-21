using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.DeviceSimulator.Generation;

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
    private readonly double _minimumGammaRayApi;
    private readonly double _maximumGammaRayApi;
    private readonly double _gammaRayFormationWavelengthMetres;
    private readonly double _maximumGammaRayNoiseApi;

    /// <summary>
    /// Initialises a new instance of the
    /// <see cref="RandomTelemetryReadingGenerator"/> class.
    /// </summary>
    /// <param name="timeProvider">Provides the current UTC time.</param>
    /// <param name="random">Provides normalised random values.</param>
    /// <param name="minimumPressurePsi">Minimum generated pressure.</param>
    /// <param name="maximumPressurePsi">Maximum generated pressure.</param>
    /// <param name="minimumTemperatureCelsius">
    /// Minimum generated temperature.
    /// </param>
    /// <param name="maximumTemperatureCelsius">
    /// Maximum generated temperature.
    /// </param>
    /// <param name="minimumGammaRayApi">
    /// Minimum generated gamma ray, in gAPI.
    /// </param>
    /// <param name="maximumGammaRayApi">
    /// Maximum generated gamma ray, in gAPI.
    /// </param>
    /// <param name="gammaRayFormationWavelengthMetres">
    /// Formation wavelength, in metres, controlling the depth-correlated
    /// gamma ray profile.
    /// </param>
    /// <param name="maximumGammaRayNoiseApi">
    /// Maximum noise, in gAPI, applied around the formation value.
    /// </param>
    internal RandomTelemetryReadingGenerator(
        TimeProvider timeProvider,
        Random random,
        double minimumPressurePsi,
        double maximumPressurePsi,
        double minimumTemperatureCelsius,
        double maximumTemperatureCelsius,
        double minimumGammaRayApi,
        double maximumGammaRayApi,
        double gammaRayFormationWavelengthMetres,
        double maximumGammaRayNoiseApi)
    {
        _timeProvider = timeProvider;
        _random = random;
        _minimumPressurePsi = minimumPressurePsi;
        _maximumPressurePsi = maximumPressurePsi;
        _minimumTemperatureCelsius = minimumTemperatureCelsius;
        _maximumTemperatureCelsius = maximumTemperatureCelsius;
        _minimumGammaRayApi = minimumGammaRayApi;
        _maximumGammaRayApi = maximumGammaRayApi;
        _gammaRayFormationWavelengthMetres = gammaRayFormationWavelengthMetres;
        _maximumGammaRayNoiseApi = maximumGammaRayNoiseApi;
    }

    /// <summary>
    /// Generates a telemetry reading for the specified device at the given
    /// measured depth.
    /// </summary>
    /// <param name="deviceId">Identifier of the device.</param>
    /// <param name="measuredDepthMetres">
    /// Measured depth, in metres, at which the reading is acquired.
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
            PressurePsi = GenerateValue(_minimumPressurePsi, _maximumPressurePsi),
            TemperatureCelsius = GenerateValue(_minimumTemperatureCelsius, _maximumTemperatureCelsius),
            GammaRayApi = GenerateGammaRay(measuredDepthMetres),
            TimestampUtc = _timeProvider.GetUtcNow(),
        };
    }

    /// <summary>
    /// Produces a depth-correlated gamma ray value with small random noise.
    /// </summary>
    /// <param name="measuredDepthMetres">
    /// Measured depth driving the formation profile.
    /// </param>
    /// <returns>The synthetic gamma ray value, in gAPI.</returns>
    private double GenerateGammaRay(double measuredDepthMetres)
    {
        double range = _maximumGammaRayApi - _minimumGammaRayApi;
        double midpoint = _minimumGammaRayApi + range / 2;
        double amplitude = range / 2;
        double phase = Math.Tau * measuredDepthMetres /
            _gammaRayFormationWavelengthMetres;
        double formationValue = midpoint + amplitude * Math.Sin(phase);
        double noise = GenerateValue(
            -_maximumGammaRayNoiseApi,
            _maximumGammaRayNoiseApi);

        return Math.Clamp(
            formationValue + noise,
            _minimumGammaRayApi,
            _maximumGammaRayApi);
    }

    /// <summary>
    /// Maps a normalised random value to the specified range.
    /// </summary>
    /// <param name="minimum">Inclusive minimum value.</param>
    /// <param name="maximum">Exclusive maximum value.</param>
    /// <returns>A randomly generated value within the range.</returns>
    private double GenerateValue(double minimum, double maximum)
    {
        return minimum + (_random.NextDouble() * (maximum - minimum));
    }
}
