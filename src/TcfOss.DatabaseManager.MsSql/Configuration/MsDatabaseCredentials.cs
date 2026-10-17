using Microsoft.Data.SqlClient;

namespace TcfOss.DatabaseManager.MsSql.Configuration;

/// <summary>
/// Connection credentials for a SQL Server instance.
/// </summary>
public record MsDatabaseCredentials
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

    public int Port { get; init; } = 1433;

    public required string Username { get; init; }

    public string Password { get; init; } = "";

    public required bool Encrypt { get; init; }

    public bool TrustServerCertificate { get; init; }

    public required int ConnectionTimeout { get; init; }

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

    public string ConnectionString
    {
        get
        {
            var csb = new SqlConnectionStringBuilder()
            {
                DataSource = DataSource,
                InitialCatalog = "master",
                Encrypt = Encrypt,
                TrustServerCertificate = TrustServerCertificate,
                ConnectTimeout = ConnectionTimeout,
            };

            if (Password != "")
            {
                csb.UserID = Username;
                csb.Password = Password;
            }
            else
            {
                csb.IntegratedSecurity = true;
            }
            return csb.ConnectionString;
        }
    }

    public string GetRedactedConnectionString()
    {
        var csb = new SqlConnectionStringBuilder(ConnectionString)
        {
            Password = "*****"
        };
        return csb.ConnectionString;
    }
}
