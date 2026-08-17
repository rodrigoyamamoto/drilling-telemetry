using DrillingTelemetry.Contracts.Commands;
using DrillingTelemetry.Control.Api.Publishing;
using DrillingTelemetry.Control.Api.Requests;
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
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        return group;
    }

    private async static Task<Results<Accepted, ValidationProblem>>
        UpdateSettingsAsync(
            UpdateSimulationSettingsRequest request,
            ISimulationSettingsCommandPublisher publisher,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(publisher);

        string[] deviceIds = request.DeviceIds ?? [];

        Dictionary<string, string[]> validationErrors =
            Validate(request, deviceIds);

        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(
                validationErrors);
        }

        var command = new UpdateSimulationSettingsCommand
        {
            Version = request.Version,
            DeviceIds = deviceIds,
            PublishingIntervalMilliseconds =
                request.PublishingIntervalMilliseconds
        };

        await publisher.PublishAsync(
            command,
            cancellationToken);

        return TypedResults.Accepted((string?)null);
    }

    private static Dictionary<string, string[]> Validate(
        UpdateSimulationSettingsRequest request,
        string[] deviceIds)
    {
        var validationErrors =
            new Dictionary<string, string[]>();

        if (request.Version <= 0)
        {
            validationErrors[nameof(request.Version)] =
            [
                "Version must be greater than zero."
            ];
        }

        if (deviceIds.Length == 0)
        {
            validationErrors[nameof(request.DeviceIds)] =
            [
                "At least one device must be provided."
            ];
        }

        if (request.PublishingIntervalMilliseconds <= 0)
        {
            validationErrors[
                    nameof(request.PublishingIntervalMilliseconds)] =
                [
                    "Publishing interval must be greater than zero."
                ];
        }

        return validationErrors;
    }
}
