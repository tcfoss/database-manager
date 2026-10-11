using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Configuration;

public abstract class ConfigurationBuilder<TConfig>
    where TConfig : ConfigBase
{
    protected const int DefaultTimeoutSeconds = 30;

    public abstract ConfigParsing.Config GetDefaultConfig(string rootDirectory);

    public abstract TConfig BuildConfiguration(
        string rootDirectory,
        ConfigParsing.Config rawConfig,
        IReadEnvironmentVariables environmentVariableReader,
        bool relaxed,
        ILogger logger);
}