using System.Collections.Concurrent;

namespace DrillingTelemetry.Processor.Sequencing;

/// <summary>
/// Tracks the last telemetry sequence observed for each device.
/// </summary>
internal sealed class TelemetrySequenceTracker
{
    private readonly ConcurrentDictionary<TelemetryStreamKey, long>
        _lastSequenceNumbers = [];

    /// <summary>
    /// Records a sequence number and classifies its position relative to the
    /// last sequence observed for the device.
    /// </summary>
    /// <param name="deviceId">Identifier of the telemetry device.</param>
    /// <param name="acquisitionSessionId">
    /// Acquisition session that owns the sequence.
    /// </param>
    /// <param name="sequenceNumber">Sequence number being observed.</param>
    /// <returns>The result of observing the sequence number.</returns>
    public TelemetrySequenceObservation Observe(
        string deviceId,
        Guid acquisitionSessionId,
        long sequenceNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        if (acquisitionSessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "The acquisition session must not be empty.",
                nameof(acquisitionSessionId));
        }

        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                sequenceNumber,
                "The sequence number must be greater than zero.");
        }

        var streamKey = new TelemetryStreamKey(
            deviceId,
            acquisitionSessionId);

        while (true)
        {
            if (!_lastSequenceNumbers.TryGetValue(
                    streamKey,
                    out long previousSequenceNumber))
            {
                if (_lastSequenceNumbers.TryAdd(
                        streamKey,
                        sequenceNumber))
                {
                    return new TelemetrySequenceObservation(
                        TelemetrySequenceStatus.Baseline,
                        PreviousSequenceNumber: 0,
                        GapSize: 0);
                }

                continue;
            }

            if (sequenceNumber <= previousSequenceNumber)
            {
                TelemetrySequenceStatus status =
                    sequenceNumber == previousSequenceNumber
                        ? TelemetrySequenceStatus.Duplicate
                        : TelemetrySequenceStatus.OutOfOrder;

                return new TelemetrySequenceObservation(
                    status,
                    previousSequenceNumber,
                    GapSize: 0);
            }

            if (!_lastSequenceNumbers.TryUpdate(
                    streamKey,
                    sequenceNumber,
                    previousSequenceNumber))
            {
                continue;
            }

            long gapSize =
                sequenceNumber - previousSequenceNumber - 1;

            TelemetrySequenceStatus updatedStatus =
                gapSize == 0
                    ? TelemetrySequenceStatus.InOrder
                    : TelemetrySequenceStatus.Gap;

            return new TelemetrySequenceObservation(
                updatedStatus,
                previousSequenceNumber,
                gapSize);
        }
    }

    private readonly record struct TelemetryStreamKey(
        string DeviceId,
        Guid AcquisitionSessionId);
}

/// <summary>
/// Describes the result of observing a telemetry sequence number.
/// </summary>
/// <param name="Status">Classification of the observed sequence.</param>
/// <param name="PreviousSequenceNumber">
/// Last sequence number previously observed for the device.
/// </param>
/// <param name="GapSize">
/// Number of sequence positions skipped between the previous and current
/// sequences.
/// </param>
internal readonly record struct TelemetrySequenceObservation(
    TelemetrySequenceStatus Status,
    long PreviousSequenceNumber,
    long GapSize);

/// <summary>
/// Identifies how a telemetry sequence relates to the previously observed
/// sequence for the same device.
/// </summary>
internal enum TelemetrySequenceStatus
{
    /// <summary>
    /// The first sequence observed for a device.
    /// </summary>
    Baseline,

    /// <summary>
    /// The sequence immediately follows the previous sequence.
    /// </summary>
    InOrder,

    /// <summary>
    /// One or more sequence numbers were not observed.
    /// </summary>
    Gap,

    /// <summary>
    /// The sequence is identical to the latest observed sequence.
    /// </summary>
    Duplicate,

    /// <summary>
    /// The sequence is older than the latest observed sequence.
    /// </summary>
    OutOfOrder
}
