using DrillingTelemetry.Processor.Operations;
using DrillingTelemetry.Processor.Responses;
using Microsoft.AspNetCore.SignalR;

namespace DrillingTelemetry.Processor.Realtime;

/// <summary>
/// Broadcasts operational telemetry events through SignalR.
/// </summary>
internal sealed class SignalROperationalEventBroadcaster
    : IOperationalEventBroadcaster
{
    private const string OperationalEventReceivedEventName =
        "operationalEventReceived";

    private readonly IHubContext<TelemetryHub> _hubContext;

    /// <summary>
    /// Initialises the SignalR operational event broadcaster.
    /// </summary>
    /// <param name="hubContext">
    /// Provides access to clients connected to the telemetry hub.
    /// </param>
    public SignalROperationalEventBroadcaster(
        IHubContext<TelemetryHub> hubContext)
    {
        ArgumentNullException.ThrowIfNull(hubContext);

        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task BroadcastAsync(
        OperationalEvent operationalEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operationalEvent);

        OperationalEventResponse response =
            OperationalEventResponse.FromModel(
                operationalEvent);

        return _hubContext.Clients.All.SendAsync(
            OperationalEventReceivedEventName,
            response,
            cancellationToken);
    }
}
