using MySqlConnector;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.MySql.Configuration;

public class MyConfig : ConfigWithSchemaMapsBase<MySchemaMapping>
{
    public required DatabaseCredentials DatabaseCredentials { get; init; }
    public required Version Version { get; init; }
    public required SchemaDefaults ServerDefaults { get; init; }
    public required Dictionary<string, CharacterSetSpec> CharacterSets { get; init; }
    public Definer? DefaultDefiner { get; init; }

    public string ConnectionString
    {
        get
        {
            var csb = new MySqlConnectionStringBuilder
            {
                UserID = DatabaseCredentials.Username,
                Port = DatabaseCredentials.Port,
                Server = DatabaseCredentials.SocketPath ?? DatabaseCredentials.Host,
                Database = "INFORMATION_SCHEMA",
                Password = DatabaseCredentials.Password,
                ConnectionProtocol = DatabaseCredentials.SocketPath != null ? MySqlConnectionProtocol.Unix : MySqlConnectionProtocol.Tcp,
                MinimumPoolSize = MinPoolSize,
                MaximumPoolSize = MaxPoolSize
            };
            return csb.ConnectionString;
        }
    }
}
