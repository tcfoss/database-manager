using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.MariaDb.App;
using TcfOss.DatabaseManager.MsSql.App;
using TcfOss.DatabaseManager.MySql.App;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.App;

public static partial class BootstrapHelpers
{
    private static readonly Option<string?> s_workingDirCliOption = new("--working-dir", "-w")
    {
        Description = "Working directory for the command. Defaults to the current directory.",
        Required = false,
        Recursive = true,
    };

    private static readonly Option<string?> s_configFileCliOption = new("--config", "-c")
    {
        Description = "Path to the configuration file.",
        Required = false,
        Recursive = true,
    };

    private static readonly Option<SqlDialect?> s_sqlDialectCliOption = new("--dialect", "-d")
    {
        Description = "SQL dialect to use.",
        Required = false,
        Recursive = true,
    };

    public static void AddGlobalOptions(RootCommand rootCommand)
    {
        rootCommand.Add(s_workingDirCliOption);
        rootCommand.Add(s_configFileCliOption);
        rootCommand.Add(s_sqlDialectCliOption);
    }

    private static (string workingDirectory, FileInfo? configPath) LoadPaths(ParseResult parseResult)
    {
        string workingDirectory = parseResult.GetValue(s_workingDirCliOption) ?? Environment.CurrentDirectory;
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"Working directory not found: {workingDirectory}");
        }

        string? configPath = parseResult.GetValue(s_configFileCliOption);

        FileInfo? configInfo = StartupBase.GetConfigPath(workingDirectory, configPath);

        return (workingDirectory, configInfo);
    }

    private static DialectLoadResult LoadDialectsAndConfig(ParseResult parseResult, FileInfo? configPath)
    {
        ConfigParsing.Config? rawConfig = StartupBase.LoadRawConfig(configPath);
        SqlDialect? configDialect = rawConfig?.Dialect;
        SqlDialect? commandDialect = parseResult.GetValue(s_sqlDialectCliOption);

        SqlDialect dialect = commandDialect ?? configDialect ?? SqlDialect.Generic;
        return new DialectLoadResult(dialect, rawConfig, commandDialect, configDialect);
    }

    public static ExecutionConfig LoadExecutionConfig(ParseResult parseResult)
    {
        (string workingDirectory, FileInfo? configPath) = LoadPaths(parseResult);
        DialectLoadResult dialectLoadResult = LoadDialectsAndConfig(parseResult, configPath);

        return new ExecutionConfig
        {
            WorkingDirectory = workingDirectory,
            ConfigPath = configPath,

            CliDialect = dialectLoadResult.CommandDialect,
            ConfigDialect = dialectLoadResult.ConfigDialect,
            Dialect = dialectLoadResult.Dialect,

            RawConfig = dialectLoadResult.RawConfig
        };
    }

    public static ExecutionContext LoadExecutionContext(
        ExecutionConfig execConfig,
        bool relaxed,
        TextWriter outputWriter,
        Action<ILogger>? loggerCallback = null,
        Action<SourceManager>? sourceManagerCallback = null
    )
    {
        StartupBase startup = execConfig.Dialect switch
        {
            SqlDialect.MySql => new MyStartup(),
            SqlDialect.MariaDb => new MaStartup(),
            SqlDialect.MsSql => new MsStartup(),
            _ => new GenericStartup()
        };

        string rootDirectory = execConfig.ConfigPath?.DirectoryName ?? execConfig.WorkingDirectory;

        ConfigParsing.Config rawConfig = execConfig.RawConfig ?? startup.GetDefaultConfig(execConfig.WorkingDirectory);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
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
            logSetupAction: (innerLogger, _) =>
            {
                logger = innerLogger;
                loggerCallback?.Invoke(innerLogger);

                if (execConfig is { CliDialect: not null, ConfigDialect: not null } && execConfig.CliDialect != execConfig.ConfigDialect)
                {
                    s_dialectMismatchWarning(logger, execConfig.CliDialect.Value, execConfig.ConfigDialect.Value, execConfig.CliDialect.Value);
                }
            },
            configSetupAction: innerConfig => config = innerConfig,
            relaxed: relaxed);

        IHost host = builder.Build();
        startup.ConfigureAppServiceProvider(host.Services);

        SourceManager? sm = host.Services.GetService<SourceManager>();
        if (sm != null)
        {
            sourceManagerCallback?.Invoke(sm);
        }

        return new ExecutionContext
        {
            Out = outputWriter,
            WorkingDirectory = execConfig.WorkingDirectory,
            Logger = logger,
            Config = config,
            CommandRunner = host.Services.GetRequiredService<IRunCommands>()
        };
    }

    public record struct DialectLoadResult(
        SqlDialect Dialect,
        ConfigParsing.Config? RawConfig,
        SqlDialect? CommandDialect,
        SqlDialect? ConfigDialect
    );

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Dialect mismatch: Cli dialect = {CliDialect}, Config dialect = {ConfigDialect}. Using CLI dialect {UseDialect}. This may cause unexpected errors if the config contains dialect-specific settings.")]
    private static partial void s_dialectMismatchWarning(ILogger logger, SqlDialect cliDialect, SqlDialect configDialect, SqlDialect useDialect);
}
