using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.App;

public partial class MySqlFormatter(MyConfig config, IWriteFiles fileWriter, IParseText textParser, IFunctionNameProvider functionNameProvider, ILoadFsDefinition<MyDefinition> fsDefinitionLoader, ILogger<MySqlFormatter> logger)
    : SqlFormatter<MyConfig>(config, fileWriter, textParser, functionNameProvider, logger)
{
    private readonly ILoadFsDefinition<MyDefinition> _fsDefinitionLoader = fsDefinitionLoader;
    private readonly ILogger<MySqlFormatter> _logger = logger;


    protected override PseudoTableSet? GetPseudoTableSet(bool relaxed, string? definitionFile = null)
    {
        try
        {
            MyDefinition definition = definitionFile != null
                ? Serialization.FromJson<ParsedDefinition<MyDefinition>>(File.ReadAllText(definitionFile)).Definition
                : _fsDefinitionLoader.LoadDefinition(relaxed: relaxed);
            return definition.ToPseudoTableSet(Config.Schemas.Keys.First());
        }
        catch (Exception ex)
        {
            s_failedToLoadDefinition(_logger, ex.Message, ex);
            return null;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to load definition file: {Message}")]
    private static partial void s_failedToLoadDefinition(ILogger logger, string message, Exception? ex);
}
