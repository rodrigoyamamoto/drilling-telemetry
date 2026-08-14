using DrillingTelemetry.DeviceSimulator.Generation;
using DrillingTelemetry.DeviceSimulator.Publishing;
using DrillingTelemetry.DeviceSimulator.Simulation;
using RabbitMQ.Client;

const string rabbitMqHostName = "localhost";
const string queueName = "drilling.telemetry.readings";

const int publishingIntervalSeconds = 2;

const double fixedPressurePsi = 8250;
const double fixedTemperatureCelsius = 117.5;

const double minimumPressurePsi = 7000;
const double maximumPressurePsi = 9000;
const double minimumTemperatureCelsius = 100;
const double maximumTemperatureCelsius = 140;

string[] deviceIds =
[
    "DRILL-001",
    "DRILL-002",
    "DRILL-003"
];

var publishingInterval = TimeSpan.FromSeconds(publishingIntervalSeconds);
TimeProvider timeProvider = TimeProvider.System;

TelemetryGenerationMode generationMode = TelemetryGenerationMode.Fixed;

if (args.Length > 0 &&
    (!Enum.TryParse(
         args[0],
         ignoreCase: true,
         out generationMode) ||
     !Enum.IsDefined(generationMode)))
{
    string availableModes = string.Join(
        ", ",
        Enum.GetNames<TelemetryGenerationMode>()
            .Select(name => name.ToLowerInvariant()));

    Console.WriteLine(
        $"Unknown generation mode '{args[0]}'.");

    Console.WriteLine(
        $"Available modes: {availableModes}.");

    return;
}

string generationModeName =
    generationMode.ToString().ToLowerInvariant();

ITelemetryReadingGenerator? readingGenerator = generationMode switch
{
    TelemetryGenerationMode.Fixed =>
        new FixedTelemetryReadingGenerator(
            timeProvider,
            fixedPressurePsi,
            fixedTemperatureCelsius),

    TelemetryGenerationMode.Random =>
        new RandomTelemetryReadingGenerator(
            timeProvider,
            Random.Shared,
            minimumPressurePsi,
            maximumPressurePsi,
            minimumTemperatureCelsius,
            maximumTemperatureCelsius),

    _ => throw new InvalidOperationException(
        $"Unsupported generation mode '{generationMode}'.")
};

Console.WriteLine(
    $"Using '{generationModeName}' generation mode.");

var connectionFactory = new ConnectionFactory
{
    HostName = rabbitMqHostName
};

await using IConnection connection =
    await connectionFactory.CreateConnectionAsync();

await using IChannel channel =
    await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(
    queue: queueName,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null);

var readingPublisher = new RabbitMqTelemetryReadingPublisher(
    channel,
    queueName);

var simulation = new TelemetrySimulation(
    readingGenerator,
    readingPublisher,
    TimeProvider.System);

var cancellationTokenSource = new CancellationTokenSource();

ConsoleCancelEventHandler cancelKeyPressHandler =
    (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationTokenSource.Cancel();
    };

Console.CancelKeyPress += cancelKeyPressHandler;

Console.WriteLine(
    $"Publishing one cycle every " +
    $"{publishingInterval.TotalSeconds} seconds.");

Console.WriteLine("Press Ctrl+C to stop.");

try
{
    await simulation.RunAsync(
        deviceIds,
        publishingInterval,
        cancellationTokenSource.Token);
}
catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
{
    Console.WriteLine("Telemetry simulation stopped.");
}
finally
{
    Console.CancelKeyPress -= cancelKeyPressHandler;
    cancellationTokenSource.Dispose();
}
