using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Hosting;
using Serilog.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.Core.LibWrapper;

public static class LoggingRegistration
{
    public static (Microsoft.Extensions.Logging.ILogger logger, string? filePath) AddLogging(this IHostApplicationBuilder builder, LogSettings config)
    {
        string? logFileName = null;
        KeyValuePair<string, string>[] serilogConfig;
        if (config.Target == LogTarget.File)
        {
            string rawLogDir = config.LogDirectory;
            string logFileDir = rawLogDir.StartsWith('~')
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), rawLogDir[1..].TrimStart('/', '\\'))
                : rawLogDir;
            logFileName = Path.Combine(logFileDir, string.Format(CultureInfo.InvariantCulture, "database-manager_{0:yyyy}{0:MM}{0:dd}_{0:HH}{0:mm}{0:ss}_{0:ffffff}.json", DateTime.Now));
#if DEBUG
            // ReSharper disable once LocalizableElement
            Console.WriteLine($"Log file will be written to: {logFileName}");
#endif
            serilogConfig =
            [
                new KeyValuePair<string, string>("using:File", "Serilog.Sinks.File"),
                new KeyValuePair<string, string>("minimum-level", config.LogLevel.ToSerilogLevel().ToString()),
                new KeyValuePair<string, string>("minimum-level:override:Microsoft", "Error"),
                new KeyValuePair<string, string>("minimum-level:override:System", "Warning"),
                new KeyValuePair<string, string>("minimum-level:override:Microsoft.EntityFrameworkCore", config.DatabaseLogLevel.ToSerilogLevel().ToString()),
                new KeyValuePair<string, string>("minimum-level:override:Microsoft.EntityFrameworkCore.Database.Command", config.DatabaseLogLevel.ToSerilogLevel().ToString()),
                new KeyValuePair<string, string>("minimum-level:override:MySqlConnector", config.DatabaseLogLevel.ToSerilogLevel().ToString()),
                new KeyValuePair<string, string>("write-to:File.path", logFileName),
                new KeyValuePair<string, string>("write-to:File.formatter", "Serilog.Formatting.Json.JsonFormatter, Serilog")
            ];
        }
        else
        {
            serilogConfig =
            [
                new KeyValuePair<string, string>("using:Console", "Serilog.Sinks.Console"),
                new KeyValuePair<string, string>("minimum-level", config.LogLevel.ToSerilogLevel().ToString()),
                new KeyValuePair<string, string>("minimum-level:override:Microsoft", "Error"),
                new KeyValuePair<string, string>("minimum-level:override:System", "Warning"),
                new KeyValuePair<string, string>("minimum-level:override:Microsoft.EntityFrameworkCore", config.DatabaseLogLevel.ToSerilogLevel().ToString()),
                new KeyValuePair<string, string>("minimum-level:override:Microsoft.EntityFrameworkCore.Database.Command", config.DatabaseLogLevel.ToSerilogLevel().ToString()),
                new KeyValuePair<string, string>("minimum-level:override:MySqlConnector", config.DatabaseLogLevel.ToSerilogLevel().ToString()),
            ];
        }

        builder.Logging.ClearProviders();
        Logger logger = new LoggerConfiguration()
            .ReadFrom.KeyValuePairs(serilogConfig)
            .CreateLogger();

        builder.Services.AddSerilog(logger);

        var factory = new SerilogLoggerFactory(logger);
        return (factory.CreateLogger("CommandLineInterface"), logFileName);
    }

    public static Microsoft.Extensions.Logging.ILogger GetBootstrapLogger()
    {
        ReloadableLogger logger = new LoggerConfiguration().MinimumLevel.Warning().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture).CreateBootstrapLogger();
        var factory = new SerilogLoggerFactory(logger);
        return factory.CreateLogger("CommandLineInterface");
    }

    private static Serilog.Events.LogEventLevel ToSerilogLevel(this LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => Serilog.Events.LogEventLevel.Verbose,
            LogLevel.Debug => Serilog.Events.LogEventLevel.Debug,
            LogLevel.Information => Serilog.Events.LogEventLevel.Information,
            LogLevel.Warning => Serilog.Events.LogEventLevel.Warning,
            LogLevel.Error => Serilog.Events.LogEventLevel.Error,
            LogLevel.Critical => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information
        };
    }
}
