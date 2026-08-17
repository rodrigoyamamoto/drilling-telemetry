using DrillingTelemetry.Contracts.Commands;

namespace DrillingTelemetry.Control.Api.Publishing;

/// <summary>
/// Publishes simulation settings commands.
/// </summary>
internal interface ISimulationSettingsCommandPublisher
{
    /// <summary>
    /// Publishes a simulation settings command.
    /// </summary>
    /// <param name="command">
    /// Command to be published.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the publishing operation.
    /// </param>
    Task PublishAsync(
        UpdateSimulationSettingsCommand command,
        CancellationToken cancellationToken);
}
