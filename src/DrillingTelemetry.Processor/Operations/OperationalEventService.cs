using DrillingTelemetry.Processor.Persistence;
using DrillingTelemetry.Processor.Realtime;

namespace DrillingTelemetry.Processor.Operations;

/// <summary>
/// Persists operational events and notifies connected real-time clients.
/// </summary>
internal sealed class OperationalEventService
    : IOperationalEventService
{
    private readonly IOperationalEventStore _operationalEventStore;
    private readonly IOperationalEventBroadcaster _operationalEventBroadcaster;
    private readonly ILogger<OperationalEventService> _logger;

    /// <summary>
    /// Initialises the operational event service.
    /// </summary>
    /// <param name="operationalEventStore">
    /// Persists and reads operational events.
    /// </param>
    /// <param name="operationalEventBroadcaster">
    /// Publishes newly recorded events to connected clients.
    /// </param>
    /// <param name="logger">
    /// Records failures that affect real-time delivery only.
    /// </param>
    public OperationalEventService(
        IOperationalEventStore operationalEventStore,
        IOperationalEventBroadcaster operationalEventBroadcaster,
        ILogger<OperationalEventService> logger)
    {
        ArgumentNullException.ThrowIfNull(operationalEventStore);
        ArgumentNullException.ThrowIfNull(operationalEventBroadcaster);
        ArgumentNullException.ThrowIfNull(logger);

        _operationalEventStore = operationalEventStore;
        _operationalEventBroadcaster = operationalEventBroadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordAsync(
        OperationalEvent operationalEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operationalEvent);

        await _operationalEventStore.SaveAsync(
            operationalEvent,
            cancellationToken);

        try
        {
            await _operationalEventBroadcaster.BroadcastAsync(
                operationalEvent,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Operational event {EventId} was persisted but " +
                "could not be broadcast",
                operationalEvent.EventId);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OperationalEvent>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        return _operationalEventStore.GetRecentAsync(
            limit,
            cancellationToken);
    }
}
