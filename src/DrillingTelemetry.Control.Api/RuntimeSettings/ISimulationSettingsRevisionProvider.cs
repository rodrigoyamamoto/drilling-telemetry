namespace DrillingTelemetry.Control.Api.RuntimeSettings;

/// <summary>
/// Provides revisions for simulation settings updates.
/// </summary>
internal interface ISimulationSettingsRevisionProvider
{
    /// <summary>
    /// Gets the next simulation settings revision.
    /// </summary>
    /// <returns>
    /// A revision greater than every revision previously returned by this provider.
    /// </returns>
    long GetNextRevision();
}
