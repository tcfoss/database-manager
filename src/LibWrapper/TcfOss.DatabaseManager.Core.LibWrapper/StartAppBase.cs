using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.LibWrapper;

public abstract partial class StartAppBase<TStartResult>
{
    public abstract TStartResult Start(
        string? configFilePath = null,
        Config? rawConfig = null,
        string? workingDirectory = null,
        bool relaxed = false,
        StartupOptions? options = null);

    protected StartResult StartCommon(
        string? configPath,
        Config? rawConfig,
        string? workingDirectory,
        bool relaxed = false,
        StartupOptions? options = null
    )
    {
        workingDirectory ??= Directory.GetCurrentDirectory();
        FileInfo? configFileInfo = StartupBase.GetConfigPath(workingDirectory, configPath);

        rawConfig ??= StartupBase.LoadRawConfig(configFileInfo);

        SqlDialect dialect = rawConfig?.Dialect ?? Dialect;

        StartupBase startup = GetStartup();

        string rootDirectory = rawConfig?.ProjectDirectory ?? configFileInfo?.DirectoryName ?? workingDirectory;

        bool usingDefaultConfig = rawConfig == null;
        rawConfig ??= startup.GetDefaultConfig(rootDirectory);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings()
        {
            ApplicationName = "TcfOss.DatabaseManager",
            ContentRootPath = rootDirectory,
            DisableDefaults = true
        });

        ConfigBase config = null!;
        ILogger logger = null!;

        startup.Configure(
            rootDirectory,
            rawConfig: rawConfig,
            builder: builder,
            environmentVariableReader: new EnvironmentVariableReader(),
            loggingSetupFunc: LoggingRegistration.AddLogging,
            configSetupAction: c => config = c,
            logSetupAction: (l, _) => logger = l,
            relaxed: relaxed
        );

        if (dialect != Dialect)
        {
            LogDialectMismatch(logger, dialect, Dialect);
        }

        options?.ConfigureBuilder?.Invoke(builder);
        options?.ConfigureServices?.Invoke(builder.Services);

        IHost host = builder.Build();
        startup.ConfigureAppServiceProvider(host.Services);

        return new StartResult
        {
            Config = config,
            Services = host.Services,
            UsingDefaultConfig = usingDefaultConfig
        };
    }

    protected abstract StartupBase GetStartup();

    protected abstract SqlDialect Dialect { get; }

    protected readonly record struct StartResult
    {
        public required ConfigBase Config { get; init; }
        public required IServiceProvider Services { get; init; }
        public required bool UsingDefaultConfig { get; init; }
    }

    [LoggerMessage(0, LogLevel.Warning, "The SQL dialect from the configuration file ({configDialect}) does not match the dialect associated with the Start function ({startDialect}) called. The Start dialect will be used.")]
    protected static partial void LogDialectMismatch(ILogger logger, SqlDialect? configDialect, SqlDialect startDialect);
}
