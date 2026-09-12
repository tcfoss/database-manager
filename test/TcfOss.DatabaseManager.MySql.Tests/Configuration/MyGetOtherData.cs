using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.Configuration;

public static class MyGetOtherData
{
    private static readonly SchemaDefaults s_serverDefaults = new()
    {
        Engine = "InnoDB",
        CharacterSet = "utf8mb4",
        Collation = "utf8mb4_0900_ai_ci"
    };

    private static readonly Dictionary<string, CharacterSetSpec> s_characterSets = DefaultSettings.GetCharacterSets(SqlDialect.MySql, new Version(8, 4, 6));

    private static Dictionary<string, object> GetData(DatabaseCredentials? credentials, bool databaseAvailable, IEnumerable<string> schemaNames)
    {
        var result = new Dictionary<string, object>();

        if (credentials != null)
        {
            result["DatabaseCredentials"] = credentials;
        }

        var otherElements = new Dictionary<string, object>
        {
            { "DatabaseAvailable", databaseAvailable },
            { "ServerDefaults", s_serverDefaults },
            { "CharacterSets", s_characterSets },
            { "SchemaDefaults", schemaNames.ToDictionary(name => name, _ => s_serverDefaults) },
        };

        foreach (var kvp in otherElements)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    public static Dictionary<string, object> GetData(ConfigParsing.Config config)
    {
        DatabaseCredentials? creds = null;
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

        var schemas = config.Schemas.Select(s => s.SchemaName);
        return GetData(creds!, true, schemas);
    }
}
