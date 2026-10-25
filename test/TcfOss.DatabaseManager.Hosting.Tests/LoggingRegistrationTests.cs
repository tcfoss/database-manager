using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Hosting;

namespace TcfOss.DatabaseManager.Hosting.Tests;

[Collection(typeof(ConsoleOutputCollection))]
public class LoggingRegistrationTests
{
    [CollectionDefinition(DisableParallelization = true)]
    public sealed class ConsoleOutputCollection;

    [Fact]
    public void AddLogging_Console_WritesMessagesToConsole()
    {
        TextWriter originalOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            var config = new LogSettings
            {
                Target = LogTarget.Console,
                LogLevel = LogLevel.Information,
                DatabaseLogLevel = LogLevel.Error,
            };

            (ILogger logger, _) = builder.AddLogging(config);
            using var host = builder.Build();
            var hostLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Application");

            logger.LogInformation("Returned logger event");
            hostLogger.LogInformation("Host logger event");
            logger.LogDebug("Filtered console event");

            string actual = output.ToString();
            Assert.Contains("Returned logger event", actual);
            Assert.Contains("Host logger event", actual);
            Assert.DoesNotContain("Filtered console event", actual);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

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
