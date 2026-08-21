namespace DrillingTelemetry.Processor.Processing;

/// <summary>
/// Describes a concurrent acquisition session conflict detected for a
/// single device.
/// </summary>
/// <param name="ExistingSessionId">
/// Acquisition session already considered active for the device.
/// </param>
/// <param name="ObservedSessionId">
/// Newly observed acquisition session that conflicts with the active one.
/// </param>
internal sealed record ConcurrentAcquisitionSessionConflict(
    Guid ExistingSessionId,
    Guid ObservedSessionId);

/// <summary>
/// Detects when the same device receives telemetry from different
/// acquisition sessions within a bounded activity window.
/// </summary>
/// <remarks>
/// <para>
/// The detection is process-local. It is safe under the existing
/// concurrent consumer model because all partition workers share a single
/// detector instance and every observation is serialised by a process-wide
/// lock. The lock is held only for in-memory bookkeeping and never spans
/// I/O, so contention remains negligible for the expected device count.
/// </para>
/// <para>
/// The detection does not alter the processing outcome of any reading.
/// It only produces an operational warning when a conflict is first
/// observed for a given session pair.
/// </para>
/// </remarks>
internal sealed class ConcurrentAcquisitionSessionDetector
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _activityWindow;
    private readonly Dictionary<string, DeviceAcquisitionState>
        _stateByDevice = [];
    private readonly object _gate = new();

    /// <summary>
    /// Initialises the concurrent acquisition session detector.
    /// </summary>
    /// <param name="timeProvider">
    /// Provides the UTC time used to evaluate the activity window.
    /// </param>
    /// <param name="activityWindow">
    /// Period within which two different sessions for the same device are
    /// considered concurrent.
    /// </param>
    public ConcurrentAcquisitionSessionDetector(
        TimeProvider timeProvider,
        TimeSpan activityWindow)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (activityWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activityWindow),
                activityWindow,
                "The activity window must be greater than zero.");
        }

        _timeProvider = timeProvider;
        _activityWindow = activityWindow;
    }

    /// <summary>
    /// Records activity for a device and acquisition session, returning a
    /// conflict when a different session is still active within the
    /// configured window.
    /// </summary>
    /// <param name="deviceId">Identifier of the telemetry device.</param>
    /// <param name="acquisitionSessionId">
    /// Acquisition session that produced the current reading.
    /// </param>
    /// <returns>
    /// A conflict when a different active session was recently observed for
    /// the same device and the conflict has not already been suppressed;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    public ConcurrentAcquisitionSessionConflict? Observe(
        string deviceId,
        Guid acquisitionSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        if (acquisitionSessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "The acquisition session must not be empty.",
                nameof(acquisitionSessionId));
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            if (!_stateByDevice.TryGetValue(
                    deviceId,
                    out DeviceAcquisitionState? state))
            {
                state = new DeviceAcquisitionState();
                _stateByDevice.Add(deviceId, state);
            }

            RemoveInactiveSessions(state, now);

            KeyValuePair<Guid, DateTimeOffset>? existingSession = null;

            foreach (KeyValuePair<Guid, DateTimeOffset> session in
                state.LastObservedBySession)
            {
                if (session.Key != acquisitionSessionId &&
                    (!existingSession.HasValue ||
                     session.Value > existingSession.Value.Value))
                {
                    existingSession = session;
                }
            }

            state.LastObservedBySession[acquisitionSessionId] = now;

            if (!existingSession.HasValue)
            {
                return null;
            }

            AcquisitionSessionPair sessionPair =
                AcquisitionSessionPair.Create(
                    existingSession.Value.Key,
                    acquisitionSessionId);

            if (!state.ReportedConflicts.Add(sessionPair))
            {
                return null;
            }

            return new ConcurrentAcquisitionSessionConflict(
                existingSession.Value.Key,
                acquisitionSessionId);
        }
    }

    private void RemoveInactiveSessions(
        DeviceAcquisitionState state,
        DateTimeOffset now)
    {
        Guid[] inactiveSessionIds = state.LastObservedBySession
            .Where(pair => now - pair.Value > _activityWindow)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (Guid sessionId in inactiveSessionIds)
        {
            state.LastObservedBySession.Remove(sessionId);
            state.ReportedConflicts.RemoveWhere(
                pair => pair.Contains(sessionId));
        }
    }

    private sealed class DeviceAcquisitionState
    {
        public Dictionary<Guid, DateTimeOffset> LastObservedBySession
        {
            get;
        } = [];

        public HashSet<AcquisitionSessionPair> ReportedConflicts
        {
            get;
        } = [];
    }

    private readonly record struct AcquisitionSessionPair(
        Guid FirstSessionId,
        Guid SecondSessionId)
    {
        public static AcquisitionSessionPair Create(
            Guid firstSessionId,
            Guid secondSessionId)
        {
            return firstSessionId.CompareTo(secondSessionId) <= 0
                ? new AcquisitionSessionPair(
                    firstSessionId,
                    secondSessionId)
                : new AcquisitionSessionPair(
                    secondSessionId,
                    firstSessionId);
        }

        public bool Contains(Guid sessionId)
        {
            return FirstSessionId == sessionId ||
                SecondSessionId == sessionId;
        }
    }
}
