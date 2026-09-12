using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MariaDb.Tests.Configuration;

public static class MaGetOtherData
{
    private static readonly DatabaseCredentials s_defaultCredentials = new()
    {
        Host = "localhost",
        Port = 3306,
        Username = "test_user",
        Password = "test_password",
        ConnectionTimeout = 30
    };

    private static readonly SchemaDefaults s_serverDefaults = new()
    {
        Engine = "InnoDB",
        CharacterSet = "utf8mb4",
        Collation = "utf8mb4_uca1400_ai_ci"
    };

    private static readonly Dictionary<string, CharacterSetSpec> s_characterSets1 = DefaultSettings.GetCharacterSets(SqlDialect.MariaDb, new Version(10, 11, 13));
    private static readonly Dictionary<string, CharacterSetSpec> s_characterSets2 = DefaultSettings.GetCharacterSets(SqlDialect.MariaDb, new Version(11, 8, 2));

    private static Dictionary<string, object> GetData(DatabaseCredentials credentials, bool databaseAvailable, IEnumerable<string> schemaNames, int majorVersion)
    {
        var characterSets = majorVersion switch
        {
            10 => s_characterSets1,
            11 => s_characterSets2,
            _ => throw new ArgumentException("Unsupported major version", nameof(majorVersion)),
        };

        return new Dictionary<string, object>
        {
            { "DatabaseCredentials", credentials },
            { "DatabaseAvailable", databaseAvailable },
            { "ServerDefaults", s_serverDefaults },
            { "CharacterSets", characterSets },
            { "SchemaDefaults", schemaNames.ToDictionary(name => name, _ => s_serverDefaults) },
        };
    }

    public static Dictionary<string, object> GetData(ConfigParsing.Config config)
    {
        DatabaseCredentials? creds;
        if (config.Credentials != null)
        {
            creds = new DatabaseCredentials
            {
                Host = config.Credentials!.Hostname,
                Port = config.Credentials.Port != null ? uint.Parse(config.Credentials.Port) : 3306,
                Username = config.Credentials.Username!,
                Password = config.Credentials.Password,
                ConnectionTimeout = config.Credentials.ConnectionTimeout != null ? (uint)config.Credentials.ConnectionTimeout : 30,
            };
        }
        else
        {
            creds = s_defaultCredentials;
        }

        var schemas = config.Schemas.Select(s => s.SchemaName);
        return GetData(creds, true, schemas, 11);
    }
}
