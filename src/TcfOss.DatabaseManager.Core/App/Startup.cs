using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.App;

public abstract class Startup<TConfig> : StartupBase
    where TConfig : ConfigBase
{
    protected abstract void RegisterServices(IHostApplicationBuilder builder, TConfig config);

    public abstract TConfig BuildConfiguration(string rootDirectory,
        ConfigParsing.Config rawConfig,
        IReadEnvironmentVariables environmentVariableReader,
        bool relaxed,
        ILogger logger);

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
