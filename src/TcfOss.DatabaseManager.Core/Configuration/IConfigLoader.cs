using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Configuration;

public interface IConfigLoader<out TConfig, TSchemaMapping>
    where TConfig : ConfigWithSchemaMapsBase<TSchemaMapping>
    where TSchemaMapping : ISchemaMapping
{
    TConfig LoadConfig(
        string configDirectory,
        ConfigParsing.Config rawConfig,
        Dictionary<string, object> otherInformation,
        bool relaxed = false
    );
}
