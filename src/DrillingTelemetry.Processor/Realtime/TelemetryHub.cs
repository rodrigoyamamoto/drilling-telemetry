using Microsoft.AspNetCore.SignalR;

namespace DrillingTelemetry.Processor.Realtime;

/// <summary>
/// Provides the real-time connection used to deliver telemetry readings.
/// </summary>
internal sealed class TelemetryHub : Hub
{
    /// <summary>
    /// Gets the route used by clients to connect to the telemetry hub.
    /// </summary>
    internal const string RoutePattern = "/hubs/telemetry";
}
