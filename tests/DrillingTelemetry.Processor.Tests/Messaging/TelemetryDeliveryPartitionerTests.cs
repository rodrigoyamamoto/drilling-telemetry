using System.Collections.Concurrent;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.Processor.Messaging;

namespace DrillingTelemetry.Processor.Tests.Messaging;

/// <summary>
/// Tests stream ordering and concurrency in telemetry delivery partitions.
/// </summary>
public sealed class TelemetryDeliveryPartitionerTests
{
    private static readonly Guid AcquisitionSessionId =
        Guid.Parse("5c97dc7e-95bc-45a1-a9cc-32629e15546f");

    private static readonly TimeSpan TestTimeout =
        TimeSpan.FromSeconds(5);

    /// <summary>
    /// Verifies that one stream cannot begin its next delivery while the
    /// previous delivery is still being processed.
    /// </summary>
    [Fact]
    public async Task RunAsync_SameStream_ProcessesDeliveriesSequentially()
    {
        // Arrange
        var partitioner = new TelemetryDeliveryPartitioner(
            partitionCount: 4,
            partitionCapacity: 4);

        using var cancellationSource =
            new CancellationTokenSource(TestTimeout);

        var firstStarted = CreateSignal();
        var releaseFirst = CreateSignal();
        var secondStarted = CreateSignal();
        var processingOrder = new ConcurrentQueue<long>();

        Task processingTask = partitioner.RunAsync(
            async (delivery, cancellationToken) =>
            {
                if (delivery.Reading.SequenceNumber == 1)
                {
                    firstStarted.TrySetResult();

                    await releaseFirst.Task.WaitAsync(
                        cancellationToken);
                }

                processingOrder.Enqueue(
                    delivery.Reading.SequenceNumber);

                if (delivery.Reading.SequenceNumber == 2)
                {
                    secondStarted.TrySetResult();
                }
            },
            cancellationSource.Token);

        await partitioner.EnqueueAsync(
            CreateDelivery("DRILL-001", sequenceNumber: 1),
            cancellationSource.Token);

        await partitioner.EnqueueAsync(
            CreateDelivery("DRILL-001", sequenceNumber: 2),
            cancellationSource.Token);

        await firstStarted.Task.WaitAsync(
            cancellationSource.Token);

        // Act
        Task completedBeforeRelease = await Task.WhenAny(
            secondStarted.Task,
            Task.Delay(
                TimeSpan.FromMilliseconds(100),
                cancellationSource.Token));

        // Assert
        Assert.NotSame(
            secondStarted.Task,
            completedBeforeRelease);

        releaseFirst.TrySetResult();

        await secondStarted.Task.WaitAsync(
            cancellationSource.Token);

        Assert.Equal(
            [1L, 2L],
            processingOrder);

        await cancellationSource.CancelAsync();
        await processingTask;
    }

    /// <summary>
    /// Verifies that independent streams assigned to different partitions
    /// can make progress concurrently.
    /// </summary>
    [Fact]
    public async Task RunAsync_IndependentStreams_ProcessesConcurrently()
    {
        // Arrange
        const int partitionCount = 2;

        var partitioner = new TelemetryDeliveryPartitioner(
            partitionCount,
            partitionCapacity: 2);

        string firstDeviceId = "DRILL-001";

        string secondDeviceId = FindDeviceInDifferentPartition(
            firstDeviceId,
            partitionCount);

        using var cancellationSource =
            new CancellationTokenSource(TestTimeout);

        var firstStarted = CreateSignal();
        var releaseFirst = CreateSignal();
        var secondStarted = CreateSignal();

        Task processingTask = partitioner.RunAsync(
            async (delivery, cancellationToken) =>
            {
                if (delivery.Reading.DeviceId == firstDeviceId)
                {
                    firstStarted.TrySetResult();

                    await releaseFirst.Task.WaitAsync(
                        cancellationToken);
                }
                else if (delivery.Reading.DeviceId == secondDeviceId)
                {
                    secondStarted.TrySetResult();
                }
            },
            cancellationSource.Token);

        await partitioner.EnqueueAsync(
            CreateDelivery(firstDeviceId, sequenceNumber: 1),
            cancellationSource.Token);

        await firstStarted.Task.WaitAsync(
            cancellationSource.Token);

        // Act
        await partitioner.EnqueueAsync(
            CreateDelivery(secondDeviceId, sequenceNumber: 1),
            cancellationSource.Token);

        await secondStarted.Task.WaitAsync(
            cancellationSource.Token);

        // Assert
        Assert.False(releaseFirst.Task.IsCompleted);

        releaseFirst.TrySetResult();

        await cancellationSource.CancelAsync();
        await processingTask;
    }

    /// <summary>
    /// Creates a completion signal whose continuations run asynchronously.
    /// </summary>
    /// <returns>A completion signal for coordinating the test.</returns>
    private static TaskCompletionSource CreateSignal()
    {
        return new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Creates a valid telemetry delivery for the specified stream.
    /// </summary>
    /// <param name="deviceId">Telemetry device identifier.</param>
    /// <param name="sequenceNumber">Device sequence number.</param>
    /// <returns>A telemetry delivery awaiting partitioned processing.</returns>
    private static TelemetryDelivery CreateDelivery(
        string deviceId,
        long sequenceNumber)
    {
        var reading = new TelemetryReading
        {
            DeviceId = deviceId,
            AcquisitionSessionId = AcquisitionSessionId,
            SequenceNumber = sequenceNumber,
            PressurePsi = 8250,
            TemperatureCelsius = 117.5,
            TimestampUtc = new DateTimeOffset(
                2026,
                8,
                20,
                12,
                0,
                0,
                TimeSpan.Zero)
        };

        return new TelemetryDelivery(
            reading,
            DeliveryTag: checked((ulong)sequenceNumber));
    }

    /// <summary>
    /// Finds a device identifier assigned to the other test partition.
    /// </summary>
    /// <param name="firstDeviceId">
    /// Device identifier whose partition must not be reused.
    /// </param>
    /// <param name="partitionCount">Number of available partitions.</param>
    /// <returns>A device identifier in a different partition.</returns>
    private static string FindDeviceInDifferentPartition(
        string firstDeviceId,
        int partitionCount)
    {
        int firstPartition = GetPartitionIndex(
            firstDeviceId,
            partitionCount);

        return Enumerable
            .Range(2, 100)
            .Select(number => $"DRILL-{number:000}")
            .First(deviceId =>
                GetPartitionIndex(deviceId, partitionCount) !=
                firstPartition);
    }

    /// <summary>
    /// Reproduces stream partition selection for deterministic test data.
    /// </summary>
    /// <param name="deviceId">Telemetry device identifier.</param>
    /// <param name="partitionCount">Number of available partitions.</param>
    /// <returns>The partition assigned to the stream.</returns>
    private static int GetPartitionIndex(
        string deviceId,
        int partitionCount)
    {
        int streamHash = HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(deviceId),
            AcquisitionSessionId);

        return (int)((uint)streamHash % (uint)partitionCount);
    }
}
