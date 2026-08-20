using DrillingTelemetry.Processor.Operations;
using DrillingTelemetry.Processor.Responses;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DrillingTelemetry.Processor.Endpoints;

/// <summary>
/// Defines operational telemetry event endpoints.
/// </summary>
internal static class OperationalEventsEndpoints
{
    private const int DefaultEventLimit = 20;
    private const int MaximumEventLimit = 100;

    /// <summary>Maps the operational event endpoints.</summary>
    /// <param name="endpoints">
    /// Application endpoint route builder.
    /// </param>
    /// <returns>The configured operational event route group.</returns>
    public static RouteGroupBuilder MapOperationalEventsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints
            .MapGroup("/api/telemetry/events")
            .WithTags("Operational events");

        group.MapGet(string.Empty, GetRecentEventsAsync)
            .WithName("GetRecentOperationalEvents")
            .WithSummary("Gets recent operational telemetry events.")
            .WithDescription(
                "Returns detected ordering, idempotency and validation events " +
                "in reverse chronological order.")
            .Produces<OperationalEventResponse[]>(
                StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<Results<Ok<OperationalEventResponse[]>, ValidationProblem>>
        GetRecentEventsAsync(
            IOperationalEventService operationalEventService,
            CancellationToken cancellationToken,
            int limit = DefaultEventLimit)
    {
        ArgumentNullException.ThrowIfNull(
            operationalEventService);

        if (limit is < 1 or > MaximumEventLimit)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(limit)] =
                    [
                        $"The event limit must be between 1 and " +
                        $"{MaximumEventLimit}."
                    ]
                });
        }

        IReadOnlyList<OperationalEvent> operationalEvents =
            await operationalEventService.GetRecentAsync(
                limit,
                cancellationToken);

        OperationalEventResponse[] response = operationalEvents
            .Select(OperationalEventResponse.FromModel)
            .ToArray();

        return TypedResults.Ok(response);
    }
}
