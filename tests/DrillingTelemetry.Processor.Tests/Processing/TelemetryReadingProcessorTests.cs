using System.Diagnostics.Metrics;
using System.Text.Json;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.Processor.Diagnostics;
using DrillingTelemetry.Processor.Persistence;
using DrillingTelemetry.Processor.Processing;
using DrillingTelemetry.Processor.Realtime;
using DrillingTelemetry.Processor.Sequencing;
using Microsoft.Extensions.Logging.Abstractions;

namespace DrillingTelemetry.Processor.Tests.Processing;

/// <summary>
/// Tests the sequence policy applied before readings reach real-time clients.
/// </summary>
public sealed class TelemetryReadingProcessorTests
{
    private static readonly Guid AcquisitionSessionId =
        Guid.Parse("3ef44c4f-7944-4c8d-8358-6faf73419d21");

    /// <summary>
    /// Verifies that a baseline and its next sequence are both broadcast.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_SequentialReadings_BroadcastsBoth()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var broadcaster = new RecordingTelemetryReadingBroadcaster();

        TelemetryReadingProcessor processor = CreateProcessor(
            meterFactory,
            broadcaster);

        TelemetryReading firstReading = CreateReading(
            deviceId: "DRILL-001",
            sequenceNumber: 1);

        TelemetryReading secondReading = CreateReading(
            deviceId: "DRILL-001",
            sequenceNumber: 2);

        // Act
        await processor.ProcessAsync(
            firstReading,
            CancellationToken.None);

        await processor.ProcessAsync(
            secondReading,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            [firstReading, secondReading],
            broadcaster.Readings);
    }

    /// <summary>
    /// Verifies that the latest reading remains useful when sequences are
    /// missing between two received readings.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_SequenceGap_BroadcastsLatestReading()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var broadcaster = new RecordingTelemetryReadingBroadcaster();

        TelemetryReadingProcessor processor = CreateProcessor(
            meterFactory,
            broadcaster);

        TelemetryReading firstReading = CreateReading(
            deviceId: "DRILL-001",
            sequenceNumber: 10);

        TelemetryReading latestReading = CreateReading(
            deviceId: "DRILL-001",
            sequenceNumber: 13);

        // Act
        await processor.ProcessAsync(
            firstReading,
            CancellationToken.None);

        await processor.ProcessAsync(
            latestReading,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            [firstReading, latestReading],
            broadcaster.Readings);
    }

    /// <summary>
    /// Verifies that a duplicate sequence is not broadcast more than once.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_DuplicateReading_DoesNotBroadcastAgain()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var broadcaster = new RecordingTelemetryReadingBroadcaster();

        TelemetryReadingProcessor processor = CreateProcessor(
            meterFactory,
            broadcaster);

        TelemetryReading reading = CreateReading(
            deviceId: "DRILL-001",
            sequenceNumber: 10);

        // Act
        await processor.ProcessAsync(
            reading,
            CancellationToken.None);

        await processor.ProcessAsync(
            reading,
            CancellationToken.None);

        // Assert
        Assert.Equal([reading], broadcaster.Readings);
    }

    /// <summary>
    /// Verifies that an older sequence cannot move a real-time stream
    /// backwards.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_OutOfOrderReading_DoesNotBroadcastOlderReading()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var broadcaster = new RecordingTelemetryReadingBroadcaster();

        TelemetryReadingProcessor processor = CreateProcessor(
            meterFactory,
            broadcaster);

        TelemetryReading firstReading = CreateReading(
            deviceId: "DRILL-001",
            sequenceNumber: 10);

        TelemetryReading latestReading = CreateReading(
            deviceId: "DRILL-001",
            sequenceNumber: 12);

        TelemetryReading lateReading = CreateReading(
            deviceId: "DRILL-001",
            sequenceNumber: 11);

        // Act
        await processor.ProcessAsync(
            firstReading,
            CancellationToken.None);

        await processor.ProcessAsync(
            latestReading,
            CancellationToken.None);

        await processor.ProcessAsync(
            lateReading,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            [firstReading, latestReading],
            broadcaster.Readings);
    }

    /// <summary>
    /// Verifies that sequence progress is tracked independently for each
    /// device.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_IndependentDevices_BroadcastsBothStreams()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var broadcaster = new RecordingTelemetryReadingBroadcaster();

        TelemetryReadingProcessor processor = CreateProcessor(
            meterFactory,
            broadcaster);

        TelemetryReading firstDeviceReading = CreateReading(
            deviceId: "DRILL-001",
            sequenceNumber: 10);

        TelemetryReading secondDeviceReading = CreateReading(
            deviceId: "DRILL-002",
            sequenceNumber: 1);

        // Act
        await processor.ProcessAsync(
            firstDeviceReading,
            CancellationToken.None);

        await processor.ProcessAsync(
            secondDeviceReading,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            [firstDeviceReading, secondDeviceReading],
            broadcaster.Readings);
    }

    /// <summary>
    /// Creates the processor with real sequence and metrics services and a
    /// recording real-time boundary.
    /// </summary>
    /// <param name="meterFactory">Meter factory owned by the test.</param>
    /// <param name="broadcaster">Records accepted telemetry readings.</param>
    /// <returns>A processor isolated from RabbitMQ and SignalR.</returns>
    private static TelemetryReadingProcessor CreateProcessor(
        IMeterFactory meterFactory,
        ITelemetryReadingBroadcaster broadcaster)
    {
        return new TelemetryReadingProcessor(
            TimeProvider.System,
            new TelemetryProcessingMetrics(meterFactory),
            new TelemetrySequenceTracker(),
            new InMemoryTelemetryReadingStore(),
            broadcaster,
            NullLogger<TelemetryReadingProcessor>.Instance);
    }

    /// <summary>
    /// Creates a valid telemetry reading for a device sequence.
    /// </summary>
    /// <param name="deviceId">Telemetry device identifier.</param>
    /// <param name="sequenceNumber">Device sequence number.</param>
    /// <returns>A valid telemetry reading.</returns>
    private static TelemetryReading CreateReading(
        string deviceId,
        long sequenceNumber)
    {
        return new TelemetryReading
        {
            DeviceId = deviceId,
            AcquisitionSessionId = AcquisitionSessionId,
            SequenceNumber = sequenceNumber,
            PressurePsi = 8250,
            TemperatureCelsius = 117.5,
            TimestampUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed class RecordingTelemetryReadingBroadcaster
        : ITelemetryReadingBroadcaster
    {
        /// <summary>
        /// Gets the readings accepted for real-time publication.
        /// </summary>
        public List<TelemetryReading> Readings { get; } = [];

        /// <inheritdoc />
        public Task BroadcastAsync(
            TelemetryReading reading,
            CancellationToken cancellationToken)
        {
            Readings.Add(reading);

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryTelemetryReadingStore
        : ITelemetryReadingStore
    {
        private readonly Dictionary<(
            string DeviceId,
            Guid AcquisitionSessionId,
            long SequenceNumber),
            string> _payloads = [];

        /// <inheritdoc />
        public Task<TelemetryReadingStoreResult> StoreAsync(
            TelemetryReading reading,
            CancellationToken cancellationToken)
        {
            var key = (
                reading.DeviceId,
                reading.AcquisitionSessionId,
                reading.SequenceNumber);

            string payload = JsonSerializer.Serialize(reading);

            if (_payloads.TryAdd(key, payload))
            {
                return Task.FromResult(
                    TelemetryReadingStoreResult.Stored);
            }

            TelemetryReadingStoreResult result =
                string.Equals(
                    _payloads[key],
                    payload,
                    StringComparison.Ordinal)
                    ? TelemetryReadingStoreResult.Duplicate
                    : TelemetryReadingStoreResult.Conflict;

            return Task.FromResult(result);
        }
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        /// <inheritdoc />
        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);

            _meters.Add(meter);

            return meter;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
