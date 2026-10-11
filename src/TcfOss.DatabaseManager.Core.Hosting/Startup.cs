using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Hosting;

public abstract class Startup<TConfig> : StartupBase
    where TConfig : ConfigBase
{
    protected abstract void RegisterServices(IHostApplicationBuilder builder, TConfig config);

    protected abstract ConfigurationBuilder<TConfig> ConfigurationBuilder { get; }

    public override ConfigParsing.Config GetDefaultConfig(string rootDirectory)
    {
        return ConfigurationBuilder.GetDefaultConfig(rootDirectory);
    }

    public virtual TConfig BuildConfiguration(string rootDirectory,
        ConfigParsing.Config rawConfig,
        IReadEnvironmentVariables environmentVariableReader,
        bool relaxed,
        ILogger logger)
    {
        return ConfigurationBuilder.BuildConfiguration(rootDirectory, rawConfig, environmentVariableReader, relaxed, logger);
    }

    /// <inheritdoc/>
    public override void Configure(
        string rootDirectory,
        ConfigParsing.Config rawConfig,
        IHostApplicationBuilder builder,
        IReadEnvironmentVariables environmentVariableReader,
        Func<IHostApplicationBuilder, LogSettings, (ILogger, string?)> loggingSetupFunc,
        Action<ILogger, string?>? logSetupAction = null,
        Action<ConfigBase>? configSetupAction = null,
        bool relaxed = false
    )
    {
        LogSettings logSettings = rawConfig.Logging;
        (ILogger logger, string? logFilePath) = loggingSetupFunc(builder, logSettings);
        logSetupAction?.Invoke(logger, logFilePath);

        TConfig config = BuildConfiguration(rootDirectory, rawConfig, environmentVariableReader, relaxed, logger);
        configSetupAction?.Invoke(config);

        builder.RegisterCommonServices(config);

        RegisterServices(builder, config);
    }

    public override void ConfigureAppServiceProvider(IServiceProvider serviceProvider)
    {
        AppServiceProvider.ServiceProvider = serviceProvider;
    }
}
