namespace TcfOss.DatabaseManager.MsSql.Configuration;

/// <summary>
/// Connection credentials for a SQL Server instance.
/// </summary>
public class MsDatabaseCredentials
{
    /// <summary>Hostname or IP address of the SQL Server instance.</summary>
    public required string Host { get; init; }

    /// <summary>
    /// Named instance (e.g. <c>SQLEXPRESS</c>). When set, the data source is
    /// formatted as <c>Host\Instance</c>. Mutually exclusive with
    /// <see cref="Port"/> (named-instance connections use the SQL Server
    /// Browser service for port resolution).
    /// </summary>
    public string? Instance { get; init; }

    /// <summary>TCP port. Defaults to 1433. Ignored when <see cref="Instance"/> is set.</summary>
    public int Port { get; init; } = 1433;

    /// <summary>
    /// SQL Server login name. When <see langword="null"/>, Windows Integrated
    /// Security is used instead.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>Password for SQL Server authentication. Ignored when <see cref="Username"/> is <see langword="null"/>.</summary>
    public string? Password { get; init; }

    /// <summary>
    /// Whether to encrypt the connection. Defaults to <see langword="true"/>.
    /// Set to <see langword="false"/> only for development environments without
    /// a valid server certificate.
    /// </summary>
    public required bool Encrypt { get; init; }

    /// <summary>
    /// Whether to trust the server certificate without validation. Only
    /// applicable when <see cref="Encrypt"/> is <see langword="true"/>.
    /// </summary>
    public bool TrustServerCertificate { get; init; }

    /// <summary>Connection timeout in seconds. Defaults to 30.</summary>
    public required int ConnectionTimeout { get; init; }

    /// <summary>
    /// Builds the <c>Data Source</c> part of the connection string from
    /// <see cref="Host"/>, <see cref="Instance"/>, and <see cref="Port"/>.
    /// </summary>
    public string DataSource
    {
        get
        {
            if (Instance != null)
            {
                return $@"{Host}\{Instance}";
            }
            if (Port != 1433)
            {
                return $"{Host},{Port}";
            }
            return Host;
        }
    }
}
