using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Hosting;

public abstract class StartupBase
{
    public static FileInfo? GetConfigPath(string workingDirectory, string? configPath)
    {
        return ConfigurationFile.GetConfigPath(workingDirectory, configPath);
    }

    public static ConfigParsing.Config? LoadRawConfig(FileInfo? configPath)
    {
        return ConfigurationFile.LoadRawConfig(configPath);
    }

    public abstract ConfigParsing.Config GetDefaultConfig(string rootDirectory);

    public abstract void Configure(
        string rootDirectory,
        ConfigParsing.Config rawConfig,
        IHostApplicationBuilder builder,
        IReadEnvironmentVariables environmentVariableReader,
        Func<IHostApplicationBuilder, LogSettings, (ILogger, string?)> loggingSetupFunc,
        Action<ILogger, string?>? logSetupAction = null,
        Action<ConfigBase>? configSetupAction = null,
        bool relaxed = false);

    public abstract void ConfigureAppServiceProvider(IServiceProvider serviceProvider);
}
