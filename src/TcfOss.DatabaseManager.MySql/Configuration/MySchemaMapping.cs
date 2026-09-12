using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.MySql.Configuration;

public class MySchemaMapping : SchemaMappingBase
{
    public required SchemaDefaults SchemaDefaults { get; init; }
}
