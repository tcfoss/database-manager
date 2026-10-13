using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.MySql.Configuration;

public record MySchemaMapping : SchemaMappingBase
{
    public required SchemaDefaults SchemaDefaults { get; init; }
}
