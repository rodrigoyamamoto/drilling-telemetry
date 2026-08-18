namespace DrillingTelemetry.Control.Api.RuntimeSettings;

/// <summary>
/// Provides process-local revisions for simulation settings updates.
/// </summary>
/// <remarks>
/// This implementation requires a single Control API instance. Replace it
/// with a globally ordered revision source before scaling the API.
/// </remarks>
internal sealed class InMemorySimulationSettingsRevisionProvider
    : ISimulationSettingsRevisionProvider
{
    private long _currentRevision;

    /// <summary>
    /// Initialises the provider using the current UTC time.
    /// </summary>
    /// <param name="timeProvider">
    /// Provides the initial revision value.
    /// </param>
    public InMemorySimulationSettingsRevisionProvider(
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _currentRevision = timeProvider.GetUtcNow().UtcTicks;
    }

    /// <inheritdoc />
    public long GetNextRevision()
    {
        return Interlocked.Increment(ref _currentRevision);
    }
}
