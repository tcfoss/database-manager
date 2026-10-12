using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.Hosting;

namespace TcfOss.DatabaseManager.Core.IntegrationTests;

public static class StartupHelpers
{
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
            loggingSetupFunc: LoggingRegistration.AddLogging,
            configSetupAction: c => config = c,
            logSetupAction: null,
            relaxed: relaxed);

        IHost host = builder.Build();

        startup.ConfigureAppServiceProvider(host.Services);
        return config;
    }
}
