using System.Threading.Channels;
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

        var readingPublisher = new RecordingTelemetryReadingPublisher();

        var settings = new SimulationSettings(
            revision: 1,
            deviceIds,
            publishingInterval: TimeSpan.FromSeconds(1),
            DrillingOperation.Stationary,
            depthChangeRateMetresPerHour: 0);

        var settingsState = new SimulationSettingsState(settings);
        
        var simulation = new TelemetrySimulation(
            readingGenerator,
            readingPublisher,
            timeProvider,
            settingsState,
            CreateDrillingContext());

        // Act
        await simulation.PublishCycleAsync(
            settings,
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
                Assert.NotEqual(
                    Guid.Empty,
                    reading.AcquisitionSessionId);
            });

        Assert.Single(
            readingPublisher.PublishedReadings
                .Select(reading => reading.AcquisitionSessionId)
                .Distinct());
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

        var readingPublisher = new RecordingTelemetryReadingPublisher();

        var settingsState = new SimulationSettingsState(
            new SimulationSettings(
                revision: 1,
                deviceIds,
                publishingInterval,
                DrillingOperation.Stationary,
                depthChangeRateMetresPerHour: 0));
        
        var simulation = new TelemetrySimulation(
            readingGenerator,
            readingPublisher,
            timeProvider,
            settingsState,
            CreateDrillingContext());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        // Act
        Task simulationTask = simulation.RunAsync(cancellationTokenSource.Token);

        // Assert
        await readingPublisher.WaitUntilCountAsync(deviceIds.Length);

        Assert.Equal(
            deviceIds,
            readingPublisher.PublishedReadings
                .Select(reading => reading.DeviceId));

        Assert.False(simulationTask.IsCompleted);

        timeProvider.Advance(publishingInterval);

        await readingPublisher.WaitUntilCountAsync(expectedDeviceIds.Length);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => simulationTask);

        Assert.Equal(
            expectedDeviceIds,
            readingPublisher.PublishedReadings
                .Select(reading => reading.DeviceId));
    }

    /// <summary>
    /// Verifies that updated settings interrupt the current wait and are
    /// applied without restarting the simulation.
    /// </summary>
    [Fact]
    public async Task RunAsync_SettingsChangedDuringExecution_AppliesWithoutRestart()
    {
        // Arrange
        const double pressurePsi = 8_250;
        const double temperatureCelsius = 117.5;

        string[] initialDeviceIds =
        [
            "DRILL-001",
            "DRILL-002"
        ];

        string[] updatedDeviceIds =
        [
            "DRILL-001",
            "DRILL-002",
            "DRILL-003",
            "DRILL-004"
        ];

        string[] expectedAfterUpdate =
        [
            "DRILL-001",
            "DRILL-002",
            "DRILL-001",
            "DRILL-002",
            "DRILL-003",
            "DRILL-004"
        ];

        string[] expectedAfterUpdatedInterval =
        [
            "DRILL-001",
            "DRILL-002",
            "DRILL-001",
            "DRILL-002",
            "DRILL-003",
            "DRILL-004",
            "DRILL-001",
            "DRILL-002",
            "DRILL-003",
            "DRILL-004"
        ];

        var currentTimeUtc = new DateTimeOffset(
            year: 2026,
            month: 8,
            day: 14,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

        var timeProvider = new FakeTimeProvider(currentTimeUtc);

        var initialSettings = new SimulationSettings(
            revision: 1,
            deviceIds: initialDeviceIds,
            publishingInterval: TimeSpan.FromSeconds(30),
            DrillingOperation.Stationary,
            depthChangeRateMetresPerHour: 0);

        var updatedSettings = new SimulationSettings(
            revision: 2,
            deviceIds: updatedDeviceIds,
            publishingInterval: TimeSpan.FromMilliseconds(500),
            DrillingOperation.Stationary,
            depthChangeRateMetresPerHour: 0);

        var settingsState =
            new SimulationSettingsState(initialSettings);

        var readingGenerator = new FixedTelemetryReadingGenerator(
            timeProvider,
            pressurePsi,
            temperatureCelsius);

        var readingPublisher =
            new RecordingTelemetryReadingPublisher();

        var simulation = new TelemetrySimulation(
            readingGenerator,
            readingPublisher,
            timeProvider,
            settingsState,
            CreateDrillingContext());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        // Act
        Task simulationTask = simulation.RunAsync(
            cancellationTokenSource.Token);

        // Assert
        await readingPublisher.WaitUntilCountAsync(
            initialDeviceIds.Length);

        Assert.Equal(
            initialDeviceIds,
            readingPublisher.PublishedReadings
                .Select(reading => reading.DeviceId));

        Assert.True(settingsState.TryUpdate(updatedSettings));

        await readingPublisher.WaitUntilCountAsync(
            expectedAfterUpdate.Length);

        Assert.Equal(
            expectedAfterUpdate,
            readingPublisher.PublishedReadings
                .Select(reading => reading.DeviceId));

        await Task.Yield();

        timeProvider.Advance(
            updatedSettings.PublishingInterval);

        await readingPublisher.WaitUntilCountAsync(
            expectedAfterUpdatedInterval.Length);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => simulationTask);

        Assert.Equal(
            expectedAfterUpdatedInterval,
            readingPublisher.PublishedReadings
                .Select(reading => reading.DeviceId));

        Assert.Equal(
            updatedSettings.Revision,
            settingsState.Current.Revision);
    }

    /// <summary>
    /// Records readings published during a test.
    /// </summary>
    private static SimulationDrillingContext CreateDrillingContext()
    {
        return new SimulationDrillingContext(
            "TEST-WELL",
            "TEST-WELLBORE",
            measuredDepthMetres: 2_847.6);
    }

    private sealed class RecordingTelemetryReadingPublisher
        : ITelemetryReadingPublisher
    {
        private readonly Channel<int> _publishedCounts =
            Channel.CreateUnbounded<int>(
                new UnboundedChannelOptions
                {
                    SingleReader = true, SingleWriter = true
                });

        /// <summary>
        /// Gets the readings received by the publisher.
        /// </summary>
        public List<TelemetryReading> PublishedReadings { get; } = [];

        /// <inheritdoc />
        public async Task PublishAsync(
            TelemetryReading reading,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PublishedReadings.Add(reading);

            await _publishedCounts.Writer.WriteAsync(PublishedReadings.Count, cancellationToken);
        }

        /// <summary>
        /// Waits until the specified number of readings has been received.
        /// </summary>
        /// <param name="expectedCount">
        /// Number of readings required to complete the wait.
        /// </param>
        /// <returns>
        /// A task completed when the expected count is reached.
        /// </returns>
        public async Task WaitUntilCountAsync(int expectedCount)
        {
            int publishedCount = 0;

            while (publishedCount < expectedCount)
            {
                publishedCount = await _publishedCounts.Reader.ReadAsync();
            }
        }
    }
}
