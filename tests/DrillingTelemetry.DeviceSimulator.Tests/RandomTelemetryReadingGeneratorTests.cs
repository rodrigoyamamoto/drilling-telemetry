using DrillingTelemetry.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace DrillingTelemetry.DeviceSimulator.Tests;

/// <summary>
/// Tests the generation of telemetry readings with random measurements.
/// </summary>
public sealed class RandomTelemetryReadingGeneratorTests
{
    /// <summary>
    /// Verifies that random values are mapped to the configured measurement ranges.
    /// </summary>
    [Fact]
    public void Generate_RandomMeasurements_MapsValuesToConfiguredRanges()
    {
        // Arrange
        const string deviceId = "DRILL-001";
        const double minimumPressurePsi = 7_000;
        const double maximumPressurePsi = 9_000;
        const double minimumTemperatureCelsius = 100;
        const double maximumTemperatureCelsius = 140;

        var currentTimeUtc = new DateTimeOffset(
            year: 2026,
            month: 8,
            day: 13,
            hour: 14,
            minute: 30,
            second: 0,
            offset: TimeSpan.Zero);

        var timeProvider = new FakeTimeProvider(currentTimeUtc);

        var random = new SequenceRandom(0.25, 0.75);

        var generator = new RandomTelemetryReadingGenerator(
            timeProvider,
            random,
            minimumPressurePsi,
            maximumPressurePsi,
            minimumTemperatureCelsius,
            maximumTemperatureCelsius);

        // Act
        TelemetryReading reading = generator.Generate(deviceId);

        // Assert
        Assert.Equal(deviceId, reading.DeviceId);
        Assert.Equal(7_500, reading.PressurePsi);
        Assert.Equal(130, reading.TemperatureCelsius);
        Assert.Equal(currentTimeUtc, reading.TimestampUtc);
    }

    /// <summary>
    /// Returns a predefined sequence of random values.
    /// </summary>
    private sealed class SequenceRandom : Random
    {
        private readonly Queue<double> _values;

        /// <summary>
        /// Initializes the random source with values returned in sequence.
        /// </summary>
        /// <param name="values">Values returned by successive calls.</param>
        public SequenceRandom(params double[] values)
        {
            _values = new Queue<double>(values);
        }

        /// <inheritdoc />
        public override double NextDouble()
        {
            return _values.Dequeue();
        }
    }
}
