namespace DrillingTelemetry.WitsmlImporter.Configuration;

/// <summary>
/// Contains the parsed command-line arguments for the WITSML importer.
/// </summary>
/// <param name="FilePath">
/// Absolute or relative path to the WITSML 1.4.1.1 log file.
/// </param>
/// <param name="DeviceId">
/// Device identifier assigned to every reading produced from the file.
/// </param>
internal sealed record ImportArguments(string FilePath, string DeviceId);
