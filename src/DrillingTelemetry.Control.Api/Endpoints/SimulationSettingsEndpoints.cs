using DrillingTelemetry.Contracts.Commands;
using DrillingTelemetry.Control.Api.Publishing;
using DrillingTelemetry.Control.Api.Requests;
using DrillingTelemetry.Control.Api.Responses;
using DrillingTelemetry.Control.Api.RuntimeSettings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DrillingTelemetry.Control.Api.Endpoints;

/// <summary>
/// Defines the simulation settings HTTP endpoints.
/// </summary>
internal static class SimulationSettingsEndpoints
{
    /// <summary>
    /// Maps the simulation settings endpoints.
    /// </summary>
    /// <param name="endpoints">
    /// Application endpoint route builder.
    /// </param>
    /// <returns>
    /// The configured simulation route group.
    /// </returns>
    public static RouteGroupBuilder MapSimulationSettingsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group =
            endpoints.MapGroup("/api/simulation");

        group.MapPost("/settings", UpdateSettingsAsync)
            .WithName("UpdateSimulationSettings")
            .WithSummary("Updates the running simulation settings.")
            .WithDescription(
                "Publishes a command that updates the simulation without restarting it.")
            .Produces<UpdateSimulationSettingsResponse>(
                StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<Results<Accepted<UpdateSimulationSettingsResponse>, ValidationProblem>>
        UpdateSettingsAsync(
            UpdateSimulationSettingsRequest request,
            ISimulationSettingsRevisionProvider revisionProvider,
            ISimulationSettingsCommandPublisher publisher,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(revisionProvider);
        ArgumentNullException.ThrowIfNull(publisher);

        long revision = revisionProvider.GetNextRevision();

        var command = new UpdateSimulationSettingsCommand
        {
            Revision = revision,
            DeviceIds = request.DeviceIds,
            PublishingIntervalMilliseconds =
                request.PublishingIntervalMilliseconds,
            DrillingOperation = request.DrillingOperation,
            DepthChangeRateMetresPerHour =
                request.DepthChangeRateMetresPerHour
        };

        await publisher.PublishAsync(
            command,
            cancellationToken);

        var response = new UpdateSimulationSettingsResponse(
            revision);

        return TypedResults.Accepted(
            uri: (string?)null,
            value: response);
    }
}
