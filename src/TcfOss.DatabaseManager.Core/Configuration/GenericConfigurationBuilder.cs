using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Configuration;

public class GenericConfigurationBuilder : ConfigurationBuilder<ConfigGeneric>
{
    private const SqlDialect DefaultDialect = SqlDialect.Generic;
    private const QuoteStyle DefaultQuoteStyle = QuoteStyle.Ansi;

    public override ConfigParsing.Config GetDefaultConfig(string rootDirectory)
    {
        return new ConfigParsing.Config()
        {
            ProjectDirectory = rootDirectory,
            Catalog = "def",
            QuoteStyle = DefaultQuoteStyle,
            Dialect = DefaultDialect,
            Schemas = [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "DEFAULT_SCHEMA",
                    RootPath = "."
                }
            ]
        };
    }

    public override ConfigGeneric BuildConfiguration(string rootDirectory, ConfigParsing.Config rawConfig, IReadEnvironmentVariables environmentVariableReader, bool relaxed, ILogger logger)
    {
        var loader = new ConfigLoader(logger);
        return loader.LoadConfig(rootDirectory, rawConfig, [], relaxed);
    }

}
