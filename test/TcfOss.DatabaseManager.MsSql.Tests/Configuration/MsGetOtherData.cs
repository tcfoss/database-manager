using TcfOss.DatabaseManager.MsSql.Configuration;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.Configuration;

public static class MsGetOtherData
{
    public static readonly MsDatabaseCredentials DefaultCredentials = new()
    {
        Host = "sqlserver",
        Port = 1433,
        Username = "testuser",
        Password = "testpassword",
        Encrypt = true,
        TrustServerCertificate = false,
        ConnectionTimeout = 30,
    };

    public static Dictionary<string, object> GetData(MsDatabaseCredentials credentials, bool databaseAvailable = true)
    {
        return new Dictionary<string, object>
        {
            { "DatabaseCredentials", credentials },
            { "DatabaseAvailable", databaseAvailable },
        };
    }

    public static Dictionary<string, object> GetData(ConfigParsing.Config config, bool databaseAvailable = true)
    {
        MsDatabaseCredentials creds;
        if (config.Credentials != null)
        {
            creds = new MsDatabaseCredentials
            {
                Host = config.Credentials.Hostname,
                Port = config.Credentials.Port != null ? int.Parse(config.Credentials.Port) : 1433,
                Username = config.Credentials.Username,
                Password = config.Credentials.Password,
                Encrypt = config.Credentials.Encrypt ?? true,
                TrustServerCertificate = config.Credentials.TrustServerCertificate ?? false,
                ConnectionTimeout = config.Credentials.ConnectionTimeout ?? 30,
            };
        }
        else
        {
            creds = DefaultCredentials;
        }

        return GetData(creds, databaseAvailable);
    }
}
