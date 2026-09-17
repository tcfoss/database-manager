using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.LibWrapper;
using TcfOss.DatabaseManager.MariaDb.LibWrapper;

namespace DatabaseBenchmarkTemplate;

public static class AppStarter
{
    public static IServiceProvider GetStartedApp(ConfigSelector configSelector, Action<IServiceCollection>? configureServices = null)
    {
        var starter = new MariaDbStartApp();
        Config config = GetConfig(configSelector);
        var opts = new StartupOptions()
        {
            ConfigureServices = configureServices
        };
        return starter.Start(rawConfig: config, workingDirectory: config.ProjectDirectory, options: opts).Services;
    }

    public static Config GetConfig(ConfigSelector selector)
    {
        return selector switch
        {
            ConfigSelector.Windows => GetWinRawConfig(),
            ConfigSelector.Linux => GetLinuxRawConfig(),
            _ => throw new NotSupportedException($"Unsupported config selector: {selector}")
        };
    }

    public static Config GetLinuxRawConfig()
    {
        string rootDir = "";
        var config = new Config
        {
            ProjectDirectory = rootDir,
            Dialect = SqlDialect.MariaDb,
            Schemas = [
                new SchemaMapping()
                {
                    SchemaName = "",
                    RootPath = ""
                }
            ],
            Credentials = new Credentials
            {
                Hostname = "",
                Username = "",
                SocketPath = "/var/run/mysqld/mysqld.sock",
            }
        };
        return config;
    }

    public static Config GetWinRawConfig()
    {
        string rootDir = @"";
        var config = new Config
        {
            ProjectDirectory = rootDir,
            Dialect = SqlDialect.MariaDb,
            Schemas = [
                new SchemaMapping()
                    {
                        SchemaName = "",
                        RootPath = ""
                    }
            ],
            Credentials = new Credentials
            {
                Hostname = "",
                Username = "",
                Port = "",
            }
        };
        return config;
    }
}

public enum ConfigSelector
{
    Windows,
    Linux
}