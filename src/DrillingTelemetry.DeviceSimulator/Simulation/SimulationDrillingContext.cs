using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.DeviceSimulator.Simulation;

/// <summary>
/// Identifies the drilling location assigned to simulated readings.
/// </summary>
internal sealed class SimulationDrillingContext
{
    /// <summary>
    /// Initialises a drilling context for the simulation.
    /// </summary>
    /// <param name="wellId">Identifier of the simulated well.</param>
    /// <param name="wellName">Name of the simulated well.</param>
    /// <param name="wellboreId">Identifier of the simulated wellbore.</param>
    /// <param name="wellboreName">Name of the simulated wellbore.</param>
    /// <param name="measuredDepthMetres">
    /// Initial distance travelled along the wellbore, in metres.
    /// </param>
    public SimulationDrillingContext(
        string wellId,
        string wellName,
        string wellboreId,
        string wellboreName,
        double measuredDepthMetres)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wellId);
        ArgumentException.ThrowIfNullOrWhiteSpace(wellName);
        ArgumentException.ThrowIfNullOrWhiteSpace(wellboreId);
        ArgumentException.ThrowIfNullOrWhiteSpace(wellboreName);

        if (!double.IsFinite(measuredDepthMetres) ||
            measuredDepthMetres < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(measuredDepthMetres),
                measuredDepthMetres,
                "Measured depth must be finite and not negative.");
        }

        WellId = wellId;
        WellName = wellName;
        WellboreId = wellboreId;
        WellboreName = wellboreName;
        MeasuredDepthMetres = measuredDepthMetres;
    }

    /// <summary>
    /// Gets the identifier of the simulated well.
    /// </summary>
    public string WellId { get; }

    /// <summary>
    /// Gets the name of the simulated well.
    /// </summary>
    public string WellName { get; }

    /// <summary>
    /// Gets the identifier of the simulated wellbore.
    /// </summary>
    public string WellboreId { get; }

    /// <summary>
    /// Gets the name of the simulated wellbore.
    /// </summary>
    public string WellboreName { get; }

    /// <summary>
    /// Gets the distance travelled along the wellbore, in metres.
    /// </summary>
    public double MeasuredDepthMetres { get; private set; }

    /// <summary>
    /// Advances measured depth using the operation and signed rate that were
    /// active during the elapsed period.
    /// </summary>
    /// <param name="operation">Operation active during the elapsed period.</param>
    /// <param name="depthChangeRateMetresPerHour">
    /// Signed measured-depth change rate, in metres per hour.
    /// </param>
    /// <param name="elapsed">Time for which the operation was active.</param>
    public void Advance(
        DrillingOperation operation,
        double depthChangeRateMetresPerHour,
        TimeSpan elapsed)
    {
        if (!DrillingOperationValidation.IsValid(
                operation,
                depthChangeRateMetresPerHour))
        {
            throw new ArgumentException(
                "The drilling operation and depth-change rate are " +
                "inconsistent.",
                nameof(depthChangeRateMetresPerHour));
        }

        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsed),
                elapsed,
                "Elapsed time must not be negative.");
        }

        double depthChangeMetres =
            depthChangeRateMetresPerHour * elapsed.TotalHours;

        MeasuredDepthMetres = Math.Max(
            0,
            MeasuredDepthMetres + depthChangeMetres);
    }
}
