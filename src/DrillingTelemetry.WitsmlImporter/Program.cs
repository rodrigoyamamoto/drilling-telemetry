using DrillingTelemetry.Contracts;
using DrillingTelemetry.WitsmlImporter.Configuration;
using DrillingTelemetry.WitsmlImporter.Parsing;
using DrillingTelemetry.WitsmlImporter.Publishing;
using Microsoft.Extensions.Configuration;

const int Success = 0;
const int InvalidArguments = 1;
const int FileNotFound = 2;
const int ParseError = 3;
const int PublishError = 4;

if (!ImportArgumentParser.TryParse(
        args,
        out ImportArguments? importArguments,
        out string? argumentError))
{
    Console.Error.WriteLine(argumentError);
    Console.Error.WriteLine();
    Console.Error.WriteLine(ImportArgumentParser.GetUsage());

    return InvalidArguments;
}

if (!File.Exists(importArguments!.FilePath))
{
    Console.Error.WriteLine(
        $"File not found: {importArguments.FilePath}");

    return FileNotFound;
}

IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

RabbitMqOptions rabbitMqOptions = configuration
    .GetSection(RabbitMqOptions.SectionName)
    .Get<RabbitMqOptions>()
    ?? throw new InvalidOperationException(
        "RabbitMQ configuration section is missing.");

WitsmlLog log;

try
{
    using FileStream stream =
        File.OpenRead(importArguments.FilePath);

    var parser = new WitsmlLogParser();
    log = parser.Parse(stream);
}
catch (WitsmlParseException exception)
{
    Console.Error.WriteLine(
        $"WITSML parse error: {exception.Message}");

    return ParseError;
}

Console.WriteLine(
    $"File: {importArguments.FilePath}");
Console.WriteLine(
    $"Well: {log.NameWell} ({log.UidWell})");
Console.WriteLine(
    $"Wellbore: {log.NameWellbore} ({log.UidWellbore})");
Console.WriteLine(
    $"Index type: {log.IndexType}");
Console.WriteLine(
    $"Index curve: {log.IndexCurve}");
Console.WriteLine(
    $"Mnemonics: {string.Join(", ", log.Mnemonics)}");
Console.WriteLine(
    $"Units: {string.Join(", ", log.Units)}");
Console.WriteLine(
    $"Data rows: {log.DataRows.Count}");

Guid acquisitionSessionId = Guid.NewGuid();
IReadOnlyList<TelemetryReading> readings;

try
{
    var mapper = new TelemetryReadingMapper();
    readings = mapper.Map(
        log,
        importArguments.DeviceId,
        acquisitionSessionId);
}
catch (WitsmlParseException exception)
{
    Console.Error.WriteLine(
        $"WITSML mapping error: {exception.Message}");
    Console.Error.WriteLine(
        "The log was parsed successfully but cannot be mapped to " +
        "telemetry readings because it lacks one or more required " +
        "curves (DTIM, GR, SPP, TEMP) or uses unsupported units.");

    return ParseError;
}

try
{
    await using RabbitMqTelemetryReadingPublisher publisher =
        await RabbitMqTelemetryReadingPublisher.CreateAsync(
            rabbitMqOptions,
            CancellationToken.None);

    foreach (TelemetryReading reading in readings)
    {
        await publisher.PublishAsync(
            reading,
            CancellationToken.None);
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(
        $"Publishing error: {exception.Message}");

    return PublishError;
}

Console.WriteLine(
    $"Readings: {readings.Count}");
Console.WriteLine(
    $"Acquisition session: {acquisitionSessionId}");
Console.WriteLine(
    $"Queue: {rabbitMqOptions.TelemetryReadingsQueueName}");

return Success;
