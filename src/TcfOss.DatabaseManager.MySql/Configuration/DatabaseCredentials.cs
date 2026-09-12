using MySqlConnector;

namespace TcfOss.DatabaseManager.MySql.Configuration;

public class DatabaseCredentials
{
    public required string Host { get; init; }
    public required string Username { get; init; }
    public string? Password { get; init; }
    public string? SocketPath { get; init; }
    public uint Port { get; init; } = 3306;
    public string DefaultCharset { get; init; } = "utf8mb4";

    public required uint ConnectionTimeout { get; init; }

    public string ConnectionString
    {
        get
        {
            var csb = new MySqlConnectionStringBuilder
            {
                UserID = Username,
                Port = Port,
                Server = SocketPath ?? Host,
                Database = "INFORMATION_SCHEMA",
                Password = Password,
                ConnectionProtocol = SocketPath != null ? MySqlConnectionProtocol.Unix : MySqlConnectionProtocol.Tcp,
                CharacterSet = DefaultCharset,
                ConnectionTimeout = ConnectionTimeout
            };
            return csb.ConnectionString;
        }
    }

    public string GetRedactedConnectionString()
    {
        var csb = new MySqlConnectionStringBuilder(ConnectionString)
        {
            Password = "*****"
        };
        return csb.ConnectionString;
    }
}
