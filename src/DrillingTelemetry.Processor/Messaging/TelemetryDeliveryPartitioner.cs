using System.Threading.Channels;
using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.Processor.Messaging;

/// <summary>
/// Routes telemetry deliveries to sequential processing partitions while
/// preserving concurrency between independent streams.
/// </summary>
internal sealed class TelemetryDeliveryPartitioner
{
    private readonly Channel<TelemetryDelivery>[] _partitions;

    /// <summary>
    /// Initialises the telemetry delivery partitions.
    /// </summary>
    /// <param name="partitionCount">
    /// Number of partitions that may process independent streams concurrently.
    /// </param>
    /// <param name="partitionCapacity">
    /// Maximum number of pending deliveries in each partition.
    /// </param>
    public TelemetryDeliveryPartitioner(
        int partitionCount,
        int partitionCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            partitionCount);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            partitionCapacity);

        _partitions = Enumerable
            .Range(0, partitionCount)
            .Select(_ => CreatePartition(partitionCapacity))
            .ToArray();
    }

    /// <summary>
    /// Enqueues a delivery in the partition assigned to its telemetry stream.
    /// </summary>
    /// <param name="delivery">Delivery awaiting processing.</param>
    /// <param name="cancellationToken">
    /// Signals that enqueueing should be cancelled.
    /// </param>
    public ValueTask EnqueueAsync(
        TelemetryDelivery delivery,
        CancellationToken cancellationToken)
    {
        int partitionIndex = GetPartitionIndex(delivery.Reading);

        return _partitions[partitionIndex].Writer.WriteAsync(
            delivery,
            cancellationToken);
    }

    /// <summary>
    /// Processes every partition sequentially and all partitions concurrently.
    /// </summary>
    /// <param name="handler">
    /// Handler applied to each delivery in a partition.
    /// </param>
    /// <param name="cancellationToken">
    /// Signals that partition processing should stop.
    /// </param>
    public Task RunAsync(
        Func<TelemetryDelivery, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        Task[] workers = _partitions
            .Select(partition =>
                ProcessPartitionAsync(
                    partition.Reader,
                    handler,
                    cancellationToken))
            .ToArray();

        return Task.WhenAll(workers);
    }

    private static Channel<TelemetryDelivery> CreatePartition(
        int capacity)
    {
        return Channel.CreateBounded<TelemetryDelivery>(
            new BoundedChannelOptions(capacity)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });
    }

    private int GetPartitionIndex(TelemetryReading reading)
    {
        int streamHash = HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(reading.DeviceId),
            reading.AcquisitionSessionId);

        return (int)((uint)streamHash % (uint)_partitions.Length);
    }

    private async static Task ProcessPartitionAsync(
        ChannelReader<TelemetryDelivery> reader,
        Func<TelemetryDelivery, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (TelemetryDelivery delivery in
                           reader.ReadAllAsync(cancellationToken))
            {
                await handler(delivery, cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // The host requested a graceful shutdown.
        }
    }
}

/// <summary>
/// Couples a deserialised telemetry reading with its RabbitMQ delivery tag.
/// </summary>
/// <param name="Reading">Telemetry reading awaiting processing.</param>
/// <param name="DeliveryTag">
/// RabbitMQ delivery tag acknowledged after processing.
/// </param>
internal readonly record struct TelemetryDelivery(
    TelemetryReading Reading,
    ulong DeliveryTag);
