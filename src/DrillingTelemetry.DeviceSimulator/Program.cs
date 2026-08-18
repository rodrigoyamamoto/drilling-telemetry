using DrillingTelemetry.DeviceSimulator.Configuration;

HostApplicationBuilder builder =
    Host.CreateApplicationBuilder(args);

builder.Services.AddDeviceSimulator();

IHost host = builder.Build();

await host.RunAsync();
