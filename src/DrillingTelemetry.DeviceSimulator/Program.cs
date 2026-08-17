using DrillingTelemetry.DeviceSimulator.Configuration;

HostApplicationBuilder builder =
    Host.CreateApplicationBuilder();

if (args.Length > 0)
{
    builder.Configuration[
            $"{SimulationOptions.SectionName}:GenerationMode"] =
        args[0];
}

builder.Services.AddDeviceSimulator();

IHost host = builder.Build();

await host.RunAsync();