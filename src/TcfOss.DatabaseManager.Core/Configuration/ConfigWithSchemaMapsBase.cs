using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Configuration;

public class ConfigWithSchemaMapsBase<TSchemaMapping> : ConfigBase
    where TSchemaMapping : ISchemaMapping
{
    public Dictionary<SchemaIdentifier, TSchemaMapping> Schemas { get; init; } = [];
}
