using DrillingTelemetry.Processor.Diagnostics;
using DrillingTelemetry.Processor.Responses;
using Microsoft.AspNetCore.SignalR;

namespace DrillingTelemetry.Processor.Realtime;

/// <summary>
/// Broadcasts telemetry processing metrics through SignalR.
/// </summary>
internal sealed class SignalRTelemetryMetricsBroadcaster
    : ITelemetryMetricsBroadcaster
{
    private const string TelemetryMetricsUpdatedEventName =
        "telemetryMetricsUpdated";

    private readonly IHubContext<TelemetryHub> _hubContext;

    /// <summary>
    /// Initialises a SignalR telemetry metrics broadcaster.
    /// </summary>
    /// <param name="hubContext">
    /// Provides access to clients connected to the telemetry hub.
    /// </param>
    public SignalRTelemetryMetricsBroadcaster(
        IHubContext<TelemetryHub> hubContext)
    {
        ArgumentNullException.ThrowIfNull(hubContext);

        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task BroadcastAsync(
        TelemetryMetricsSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        TelemetryMetricsResponse response =
            TelemetryMetricsResponse.FromModel(
                snapshot);

        return _hubContext.Clients.All.SendAsync(
            TelemetryMetricsUpdatedEventName,
            response,
            cancellationToken);
    }
}
