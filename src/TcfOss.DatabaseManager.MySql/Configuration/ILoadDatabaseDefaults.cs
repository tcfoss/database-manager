using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.MySql.Configuration;

// Interface also provided for library usage
// ReSharper disable UnusedMemberInSuper.Global
public interface ILoadDatabaseDefaults
{
    public SchemaDefaults GetServerDefaults(SqlDialect dialect, Version version);
    public Dictionary<string, CharacterSetSpec> GetCharacterSets(SqlDialect dialect, Version version);
    public SchemaDefaults GetSchemaDefaults(string schemaName, string catalogName, SchemaDefaults serverDefaults);
}
