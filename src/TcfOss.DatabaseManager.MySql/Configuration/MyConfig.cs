using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.MySql.Configuration;

public record MyConfig : ConfigWithSchemaMapsBase<MySchemaMapping>
{
    public required DatabaseCredentials DatabaseCredentials { get; init; }
    public required Version Version { get; init; }
    public required SchemaDefaults ServerDefaults { get; init; }
    public required Dictionary<string, CharacterSetSpec> CharacterSets { get; init; }
    public Definer? DefaultDefiner { get; init; }

    public string ConnectionString => DatabaseCredentials.ConnectionString;
}
