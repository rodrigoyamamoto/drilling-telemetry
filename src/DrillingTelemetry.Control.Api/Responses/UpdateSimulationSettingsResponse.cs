namespace DrillingTelemetry.Control.Api.Responses;

/// <summary>
/// Represents an accepted simulation settings update.
/// </summary>
/// <param name="Revision">
/// Revision assigned to the accepted update.
/// </param>
public sealed record UpdateSimulationSettingsResponse(
    long Revision);
