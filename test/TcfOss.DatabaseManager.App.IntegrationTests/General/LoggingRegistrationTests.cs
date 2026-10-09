using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.App.IntegrationTests.General;

public class LoggingRegistrationTests
{
    private static readonly List<string> s_expectedMessages =
        ["Application event", "Database event", "Connector event"];

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.None)]
    public void AddLogging_Console_MapsLevelsAndRegistersHostLogger(LogLevel minimumLevel)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        var config = new LogSettings
        {
            Target = LogTarget.Console,
            LogLevel = minimumLevel,
            DatabaseLogLevel = LogLevel.Error,
        };

        (ILogger logger, string? filePath) = builder.AddLogging(config);
        using var host = builder.Build();
        using var serilogLogger = Assert.IsType<IDisposable>(host.Services.GetRequiredService<Serilog.ILogger>(), exactMatch: false);
        var hostLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Application");

        Assert.Null(filePath);
        LogLevel expectedMinimum = minimumLevel == LogLevel.None ? LogLevel.Information : minimumLevel;
        foreach (LogLevel level in Enum.GetValues<LogLevel>())
        {
            bool enabled = level != LogLevel.None && level >= expectedMinimum;
            Assert.Equal(enabled, logger.IsEnabled(level));
            Assert.Equal(enabled, hostLogger.IsEnabled(level));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddLogging_File_ExpandsDirectoryAndWritesJsonWithCategoryOverrides(bool tildePath)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dbman-log-tests-");
        try
        {
            string logDirectory = directory.FullName;
            if (tildePath)
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                logDirectory = "~/" + Path.GetRelativePath(home, directory.FullName);
            }
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            var config = new LogSettings
            {
                Target = LogTarget.File,
                LogDirectory = logDirectory,
                LogLevel = LogLevel.Information,
                DatabaseLogLevel = LogLevel.Error,
            };
            (ILogger logger, string? filePath) = builder.AddLogging(config);
            Assert.NotNull(filePath);
            Assert.Equal(directory.FullName, Path.GetDirectoryName(Path.GetFullPath(filePath)));
            Assert.Matches(@"^database-manager_\d{8}_\d{6}_\d{6}\.json$", Path.GetFileName(filePath));

            using (var host = builder.Build())
            {
                using var serilogLogger = Assert.IsType<IDisposable>(host.Services.GetRequiredService<Serilog.ILogger>(), exactMatch: false);
                var factory = host.Services.GetRequiredService<ILoggerFactory>();
                logger.LogInformation("Application event");
                logger.LogDebug("Filtered application event");
                factory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command").LogWarning("Filtered database event");
                factory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command").LogError("Database event");
                factory.CreateLogger("MySqlConnector").LogWarning("Filtered connector event");
                factory.CreateLogger("MySqlConnector").LogError("Connector event");
                factory.CreateLogger("Microsoft.Hosting").LogWarning("Filtered Microsoft event");
                factory.CreateLogger("System.Net").LogInformation("Filtered System event");
            }

            string[] lines = File.ReadAllLines(filePath);
            Assert.Equal(3, lines.Length);
            var messages = new List<string>();
            foreach (string line in lines)
            {
                using var document = JsonDocument.Parse(line);
                messages.Add(document.RootElement.GetProperty("MessageTemplate").GetString()!);
                Assert.True(document.RootElement.TryGetProperty("Timestamp", out _));
                Assert.True(document.RootElement.TryGetProperty("Level", out _));
            }

            Assert.Equal(s_expectedMessages, messages);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetBootstrapLogger_EnablesWarningAndAboveOnly()
    {
        var logger = LoggingRegistration.GetBootstrapLogger();

        Assert.False(logger.IsEnabled(LogLevel.Trace));
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }
}
