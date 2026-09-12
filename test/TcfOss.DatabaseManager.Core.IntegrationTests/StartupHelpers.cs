using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.IntegrationTests;

public static class StartupHelpers
{
    private static (ILogger logger, string? path) AddConsoleLogging(this IHostApplicationBuilder builder, LogSettings logSettings)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        return (new LoggerFactory().CreateLogger("TestLogger"), null);
    }

    public static ConfigBase StartApp(this StartupBase startup, string rootPath, Config rawConfig, bool relaxed = false, IReadEnvironmentVariables? environmentVariableReader = null)
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "TcfOss.DatabaseManager",
            ContentRootPath = rootPath,
            DisableDefaults = true
        });

        ConfigBase config = null!;

        startup.Configure(
            rootPath,
            rawConfig,
            builder,
            environmentVariableReader ?? new EnvironmentVariableReader(),
            loggingSetupFunc: AddConsoleLogging,
            configSetupAction: c => config = c,
            logSetupAction: null,
            relaxed: relaxed);

        IHost host = builder.Build();

        startup.ConfigureAppServiceProvider(host.Services);
        return config;
    }
}
