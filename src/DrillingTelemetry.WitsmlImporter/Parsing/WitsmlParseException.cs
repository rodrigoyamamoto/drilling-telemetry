namespace DrillingTelemetry.WitsmlImporter.Parsing;

/// <summary>
/// Thrown when a WITSML document cannot be parsed or contains data that
/// violates the supported subset.
/// </summary>
internal sealed class WitsmlParseException : Exception
{
    /// <summary>
    /// Initialises a WITSML parse exception with a descriptive message.
    /// </summary>
    /// <param name="message">
    /// Message explaining why parsing failed.
    /// </param>
    public WitsmlParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a WITSML parse exception with a descriptive message
    /// and the underlying exception.
    /// </summary>
    /// <param name="message">
    /// Message explaining why parsing failed.
    /// </param>
    /// <param name="innerException">
    /// The exception that caused the parse failure.
    /// </param>
    public WitsmlParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
