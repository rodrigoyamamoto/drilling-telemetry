namespace DrillingTelemetry.WitsmlImporter.Configuration;

/// <summary>
/// Parses the WITSML importer command-line arguments.
/// </summary>
internal static class ImportArgumentParser
{
    private const string FileArgument = "--file";
    private const string DeviceIdArgument = "--device-id";

    /// <summary>
    /// Attempts to parse the command-line arguments.
    /// </summary>
    /// <param name="args">
    /// Raw command-line arguments received by the process.
    /// </param>
    /// <param name="arguments">
    /// Receives the parsed arguments when parsing succeeds.
    /// </param>
    /// <param name="errorMessage">
    /// Receives a human-readable message when parsing fails.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the arguments are valid;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public static bool TryParse(
        IReadOnlyList<string> args,
        out ImportArguments? arguments,
        out string? errorMessage)
    {
        string? filePath = null;
        string? deviceId = null;

        for (int i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case FileArgument:
                    if (!TryReadValue(
                            args,
                            ref i,
                            FileArgument,
                            out string? fileValue,
                            out errorMessage))
                    {
                        arguments = null;
                        return false;
                    }

                    filePath = fileValue;
                    break;

                case DeviceIdArgument:
                    if (!TryReadValue(
                            args,
                            ref i,
                            DeviceIdArgument,
                            out string? deviceValue,
                            out errorMessage))
                    {
                        arguments = null;
                        return false;
                    }

                    deviceId = deviceValue;
                    break;

                default:
                    arguments = null;
                    errorMessage =
                        $"Unknown argument '{args[i]}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            arguments = null;
            errorMessage = $"{FileArgument} is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            arguments = null;
            errorMessage = $"{DeviceIdArgument} is required.";
            return false;
        }

        arguments = new ImportArguments(filePath, deviceId);
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Returns the usage string describing the expected command-line
    /// arguments.
    /// </summary>
    /// <returns>A multi-line usage message.</returns>
    public static string GetUsage()
    {
        return
            "Usage: DrillingTelemetry.WitsmlImporter " +
            "--file <path> --device-id <id>" + Environment.NewLine +
            "  --file       Path to a WITSML 1.4.1.1 log XML file." +
            Environment.NewLine +
            "  --device-id  Device identifier assigned to imported " +
            "readings.";
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string argumentName,
        out string? value,
        out string? errorMessage)
    {
        if (index + 1 >= args.Count)
        {
            value = null;
            errorMessage = $"{argumentName} requires a value.";
            return false;
        }

        value = args[++index];
        errorMessage = null;
        return true;
    }
}
