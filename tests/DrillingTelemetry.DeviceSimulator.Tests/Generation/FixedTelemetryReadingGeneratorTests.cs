using DrillingTelemetry.DeviceSimulator.Generation;
using Microsoft.Extensions.Time.Testing;

namespace DrillingTelemetry.DeviceSimulator.Tests.Generation;

/// <summary>
/// Tests the generation of telemetry readings with fixed measurements.
/// </summary>
public sealed class FixedTelemetryReadingGeneratorTests
{
    /// <summary>
    /// Verifies that the generator returns the configured measurements,
    /// device identifier, and timestamp.
    /// </summary>
    [Fact]
    public void Generate_FixedMeasurements_ReturnsConfiguredReading()
    {
        // Arrange
        const string deviceId = "DRILL-001";
        const double pressurePsi = 8_250;
        const double temperatureCelsius = 117.5;

        var currentTimeUtc = new DateTimeOffset(
            year: 2026,
            month: 8,
            day: 13,
            hour: 10,
            minute: 30,
            second: 0,
            offset: TimeSpan.Zero);

        var timeProvider = new FakeTimeProvider(currentTimeUtc);

        var generator = new FixedTelemetryReadingGenerator(
            timeProvider,
            pressurePsi,
            temperatureCelsius);

        // Act
        var reading = generator.Generate(deviceId);

        // Assert
        Assert.Equal(deviceId, reading.DeviceId);
        Assert.Equal(pressurePsi, reading.PressurePsi);
        Assert.Equal(temperatureCelsius, reading.TemperatureCelsius);
        Assert.Equal(currentTimeUtc, reading.TimestampUtc);
    }
}
