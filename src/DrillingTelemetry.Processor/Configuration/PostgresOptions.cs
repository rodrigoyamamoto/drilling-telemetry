namespace DrillingTelemetry.Processor.Configuration;

/// <summary>
/// Contains the PostgreSQL infrastructure configuration.
/// </summary>
internal sealed class PostgresOptions
{
    /// <summary>
    /// Configuration section containing the PostgreSQL settings.
    /// </summary>
    public const string SectionName = "Postgres";

    /// <summary>
    /// Gets or sets the PostgreSQL server host name.
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PostgreSQL server port.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the telemetry database name.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PostgreSQL user name.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PostgreSQL password supplied at runtime.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
