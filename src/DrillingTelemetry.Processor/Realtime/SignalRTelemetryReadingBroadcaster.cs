using DrillingTelemetry.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace DrillingTelemetry.Processor.Realtime;

/// <summary>
/// Broadcasts telemetry readings through SignalR.
/// </summary>
internal sealed class SignalRTelemetryReadingBroadcaster
    : ITelemetryReadingBroadcaster
{
    private const string TelemetryReadingReceivedEventName =
        "telemetryReadingReceived";

    private readonly IHubContext<TelemetryHub> _hubContext;

    /// <summary>
    /// Initialises a SignalR telemetry reading broadcaster.
    /// </summary>
    /// <param name="hubContext">
    /// Provides access to clients connected to the telemetry hub.
    /// </param>
    public SignalRTelemetryReadingBroadcaster(
        IHubContext<TelemetryHub> hubContext)
    {
        ArgumentNullException.ThrowIfNull(hubContext);

        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task BroadcastAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return _hubContext.Clients.All.SendAsync(
            TelemetryReadingReceivedEventName,
            reading,
            cancellationToken);
    }
}
