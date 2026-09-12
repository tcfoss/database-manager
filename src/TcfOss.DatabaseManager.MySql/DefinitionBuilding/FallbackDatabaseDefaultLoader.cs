using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public partial class FallbackDatabaseDefaultLoader(ILogger logger) : ILoadDatabaseDefaults
{
    private readonly ILogger _logger = logger;

    public SchemaDefaults GetServerDefaults(SqlDialect dialect, Version version)
    {
        s_logUsingFallbackDatabaseDefaults(_logger);
        string defaultCharset = DefaultSettings.GetDefaultCharacterSet(dialect, version);
        Dictionary<string, CharacterSetSpec> characterSets = DefaultSettings.GetCharacterSets(dialect, version);

        return new SchemaDefaults
        {
            Engine = DefaultSettings.DefaultEngine,
            CharacterSet = defaultCharset,
            Collation = characterSets[defaultCharset].DefaultCollation,
        };
    }

    public SchemaDefaults GetSchemaDefaults(string schemaName, string catalogName, SchemaDefaults serverDefaults)
    {
        return serverDefaults;
    }

    public Dictionary<string, CharacterSetSpec> GetCharacterSets(SqlDialect dialect, Version version)
    {
        s_logUsingFallbackCharacterSetMappings(_logger);
        return DefaultSettings.GetCharacterSets(dialect, version);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Using fallback database defaults. These may not match the actual server defaults.")]
    private static partial void s_logUsingFallbackDatabaseDefaults(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Using fallback character set to collation mappings. These may not match the actual server settings.")]
    private static partial void s_logUsingFallbackCharacterSetMappings(ILogger logger);
}
