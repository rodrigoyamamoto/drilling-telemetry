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
    /// <param name="wellboreId">Identifier of the simulated wellbore.</param>
    /// <param name="measuredDepthMetres">
    /// Initial distance travelled along the wellbore, in metres.
    /// </param>
    public SimulationDrillingContext(
        string wellId,
        string wellboreId,
        double measuredDepthMetres)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wellId);
        ArgumentException.ThrowIfNullOrWhiteSpace(wellboreId);

        if (measuredDepthMetres < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(measuredDepthMetres),
                measuredDepthMetres,
                "Measured depth must not be negative.");
        }

        WellId = wellId;
        WellboreId = wellboreId;
        MeasuredDepthMetres = measuredDepthMetres;
    }

    /// <summary>
    /// Gets the identifier of the simulated well.
    /// </summary>
    public string WellId { get; }

    /// <summary>
    /// Gets the identifier of the simulated wellbore.
    /// </summary>
    public string WellboreId { get; }

    /// <summary>
    /// Gets the distance travelled along the wellbore, in metres.
    /// </summary>
    public double MeasuredDepthMetres { get; }
}
