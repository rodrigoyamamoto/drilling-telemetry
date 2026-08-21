using DrillingTelemetry.Contracts;
using DrillingTelemetry.Processor.Persistence;
using DrillingTelemetry.Processor.Responses;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DrillingTelemetry.Processor.Endpoints;

/// <summary>
/// Defines the persisted telemetry HTTP endpoints.
/// </summary>
internal static class TelemetryReadingsEndpoints
{
    private const int DefaultReadingLimit = 100;
    private const int MaximumReadingLimit = 1000;

    /// <summary>
    /// Maps the persisted telemetry endpoints.
    /// </summary>
    /// <param name="endpoints">
    /// Application endpoint route builder.
    /// </param>
    /// <returns>The configured telemetry route group.</returns>
    public static RouteGroupBuilder MapTelemetryReadingsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints
            .MapGroup("/api/telemetry")
            .WithTags("Telemetry");

        group.MapGet("/devices", GetDeviceIdsAsync)
            .WithName("GetTelemetryDeviceIds")
            .WithSummary(
                "Gets devices that have persisted telemetry readings.")
            .WithDescription(
                "Returns the device identifiers available for historical queries.")
            .Produces<string[]>(StatusCodes.Status200OK);

        group.MapGet(
                "/readings/{deviceId}",
                GetReadingsAsync)
            .WithName("GetTelemetryReadings")
            .WithSummary(
                "Gets recent persisted telemetry readings for a device's latest acquisition session.")
            .WithDescription(
                "Returns the newest readings in chronological order, scoped to the latest acquisition session identified by the most recent acquisition timestamp.")
            .Produces<TelemetryReadingResponse[]>(
                StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return group;
    }

    private async static Task<Ok<string[]>> GetDeviceIdsAsync(
        ITelemetryHistoryReader telemetryHistoryReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            telemetryHistoryReader);

        IReadOnlyList<string> deviceIds =
            await telemetryHistoryReader.GetDeviceIdsAsync(
                cancellationToken);

        return TypedResults.Ok(deviceIds.ToArray());
    }

    private static async Task<Results<Ok<TelemetryReadingResponse[]>, ValidationProblem>>
        GetReadingsAsync(
            string deviceId,
            ITelemetryHistoryReader telemetryHistoryReader,
            CancellationToken cancellationToken,
            int limit = DefaultReadingLimit)
    {
        ArgumentNullException.ThrowIfNull(
            telemetryHistoryReader);

        Dictionary<string, string[]> validationErrors =
            ValidateRequest(deviceId, limit);

        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(
                validationErrors);
        }

        IReadOnlyList<TelemetryReading> readings =
            await telemetryHistoryReader.GetReadingsAsync(
                deviceId,
                limit,
                cancellationToken);

        TelemetryReadingResponse[] response = readings
            .Select(MapResponse)
            .ToArray();

        return TypedResults.Ok(response);
    }

    private static Dictionary<string, string[]> ValidateRequest(
        string deviceId,
        int limit)
    {
        Dictionary<string, string[]> validationErrors = [];

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            validationErrors[nameof(deviceId)] =
                ["A device identifier must be provided."];
        }

        if (limit is < 1 or > MaximumReadingLimit)
        {
            validationErrors[nameof(limit)] =
                [$"The reading limit must be between 1 and {MaximumReadingLimit}."];
        }

        return validationErrors;
    }

    private static TelemetryReadingResponse MapResponse(
        TelemetryReading reading)
    {
        return new TelemetryReadingResponse(
            reading.DeviceId,
            reading.AcquisitionSessionId,
            reading.SequenceNumber,
            reading.WellId,
            reading.WellName,
            reading.WellboreId,
            reading.WellboreName,
            reading.MeasuredDepthMetres,
            reading.DrillingOperation,
            reading.DepthChangeRateMetresPerHour,
            reading.PressurePsi,
            reading.TemperatureCelsius,
            reading.GammaRayApi,
            reading.TimestampUtc);
    }
}
