using DrillingTelemetry.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace DrillingTelemetry.DeviceSimulator.Tests;

/// <summary>
/// Tests the execution of telemetry simulation cycles.
/// </summary>
public sealed class TelemetrySimulationTests
{
    /// <summary>
    /// Verifies that one reading is published for each configured device.
    /// </summary>
    [Fact]
    public async Task PublishCycleAsync_ThreeDevices_PublishesOneReadingPerDevice()
    {
        // Arrange
        const double pressurePsi = 8_250;
        const double temperatureCelsius = 117.5;

        string[] deviceIds =
        [
            "DRILL-001",
            "DRILL-002",
            "DRILL-003"
        ];

        var currentTimeUtc = new DateTimeOffset(
            year: 2026,
            month: 8,
            day: 13,
            hour: 15,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

        var timeProvider = new FakeTimeProvider(currentTimeUtc);

        var readingGenerator = new FixedTelemetryReadingGenerator(
            timeProvider,
            pressurePsi,
            temperatureCelsius);

        var readingPublisher = new RecordingTelemetryReadingPublisher();

        var simulation = new TelemetrySimulation(
            readingGenerator,
            readingPublisher);

        // Act
        await simulation.PublishCycleAsync(
            deviceIds,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            deviceIds,
            readingPublisher.PublishedReadings
                .Select(reading => reading.DeviceId));

        Assert.All(
            readingPublisher.PublishedReadings,
            reading =>
            {
                Assert.Equal(pressurePsi, reading.PressurePsi);
                Assert.Equal(temperatureCelsius, reading.TemperatureCelsius);
                Assert.Equal(currentTimeUtc, reading.TimestampUtc);
            }
        );
    }

    /// <summary>
    /// Records readings published during a test.
    /// </summary>
    private sealed class RecordingTelemetryReadingPublisher : ITelemetryReadingPublisher
    {
        /// <summary>
        /// Gets the readings received by the publisher.
        /// </summary>
        public List<TelemetryReading> PublishedReadings { get; } = [];

        /// <inheritdoc />
        public Task PublishAsync(
            TelemetryReading reading,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PublishedReadings.Add(reading);

            return Task.CompletedTask;
        }
    }
}
