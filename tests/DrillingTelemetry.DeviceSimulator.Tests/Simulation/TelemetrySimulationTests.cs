using DrillingTelemetry.Contracts;
using DrillingTelemetry.DeviceSimulator.Generation;
using DrillingTelemetry.DeviceSimulator.Publishing;
using DrillingTelemetry.DeviceSimulator.Simulation;
using Microsoft.Extensions.Time.Testing;

namespace DrillingTelemetry.DeviceSimulator.Tests.Simulation;

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

        var readingPublisher =
            new RecordingTelemetryReadingPublisher(
                expectedReadingCount: deviceIds.Length);

        var simulation = new TelemetrySimulation(
            readingGenerator,
            readingPublisher,
            timeProvider);

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
                Assert.Equal(
                    temperatureCelsius,
                    reading.TemperatureCelsius);
                Assert.Equal(currentTimeUtc, reading.TimestampUtc);
            });
    }

    /// <summary>
    /// Verifies that the simulation waits for the configured interval,
    /// publishes another cycle, and supports cancellation.
    /// </summary>
    [Fact]
    public async Task RunAsync_IntervalElapsed_PublishesNextCycleUntilCancelled()
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

        string[] expectedDeviceIds =
        [
            "DRILL-001",
            "DRILL-002",
            "DRILL-003",
            "DRILL-001",
            "DRILL-002",
            "DRILL-003"
        ];

        TimeSpan publishingInterval = TimeSpan.FromSeconds(2);

        var currentTimeUtc = new DateTimeOffset(
            year: 2026,
            month: 8,
            day: 14,
            hour: 10,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

        var timeProvider = new FakeTimeProvider(currentTimeUtc);

        var readingGenerator = new FixedTelemetryReadingGenerator(
            timeProvider,
            pressurePsi,
            temperatureCelsius);

        var readingPublisher =
            new RecordingTelemetryReadingPublisher(
                expectedReadingCount: expectedDeviceIds.Length);

        var simulation = new TelemetrySimulation(
            readingGenerator,
            readingPublisher,
            timeProvider);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        // Act
        Task simulationTask = simulation.RunAsync(
            deviceIds,
            publishingInterval,
            cancellationTokenSource.Token);

        // Assert
        Assert.Equal(
            deviceIds,
            readingPublisher.PublishedReadings
                .Select(reading => reading.DeviceId));

        Assert.False(simulationTask.IsCompleted);

        timeProvider.Advance(publishingInterval);

        await readingPublisher.WaitUntilExpectedCountAsync();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => simulationTask);

        Assert.Equal(
            expectedDeviceIds,
            readingPublisher.PublishedReadings
                .Select(reading => reading.DeviceId));
    }

    /// <summary>
    /// Records readings published during a test.
    /// </summary>
    private sealed class RecordingTelemetryReadingPublisher
        : ITelemetryReadingPublisher
    {
        private readonly int _expectedReadingCount;

        private readonly TaskCompletionSource<bool>
            _expectedCountReached = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Initializes a publisher that signals when the expected number
        /// of readings has been received.
        /// </summary>
        /// <param name="expectedReadingCount">
        /// Number of readings required to complete the signal.
        /// </param>
        public RecordingTelemetryReadingPublisher(
            int expectedReadingCount)
        {
            _expectedReadingCount = expectedReadingCount;
        }

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

            if (PublishedReadings.Count == _expectedReadingCount)
            {
                _expectedCountReached.TrySetResult(true);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Waits until the expected number of readings has been received.
        /// </summary>
        /// <returns>
        /// A task completed when the expected count is reached.
        /// </returns>
        public Task WaitUntilExpectedCountAsync()
        {
            return _expectedCountReached.Task;
        }
    }
}