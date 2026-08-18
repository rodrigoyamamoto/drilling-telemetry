namespace DrillingTelemetry.Contracts;

/// <summary>
/// Defines operational limits shared by simulation inputs.
/// </summary>
public static class SimulationLimits
{
    /// <summary>
    /// Minimum interval allowed between telemetry publishing cycles.
    /// </summary>
    public const int MinimumPublishingIntervalMilliseconds = 50;
}
