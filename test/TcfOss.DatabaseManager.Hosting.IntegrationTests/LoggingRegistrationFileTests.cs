using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.Core.IntegrationTests;

namespace TcfOss.DatabaseManager.Hosting.IntegrationTests;

public class LoggingRegistrationFileTests(FsProjectFixture fixture) : IClassFixture<FsProjectFixture>
{
    private static readonly List<string> s_expectedMessages =
        ["Application event", "Database event", "Connector event"];

    private FsProjectFixture Fixture { get; } = fixture;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddLogging_File_ExpandsDirectoryAndWritesJsonWithCategoryOverrides(bool tildePath)
    {
        string directory = CreateLogDirectory();
        string logDirectory = directory;
        if (tildePath)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            logDirectory = "~/" + Path.GetRelativePath(home, directory);
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
        Assert.Equal(directory, Path.GetDirectoryName(Path.GetFullPath(filePath)));
        Assert.Matches(@"^database-manager_\d{8}_\d{6}_\d{6}\.json$", Path.GetFileName(filePath));

        using (var host = builder.Build())
        {
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
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(stream.Length > 0);
        }
        File.Delete(filePath);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task AddLogging_File_AsyncHostDisposalReleasesFile()
    {
        string directory = CreateLogDirectory();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        var config = new LogSettings
        {
            Target = LogTarget.File,
            LogDirectory = directory,
            LogLevel = LogLevel.Information
        };
        (ILogger logger, string? filePath) = builder.AddLogging(config);
        Assert.NotNull(filePath);

        await using (_ = Assert.IsType<IAsyncDisposable>(builder.Build(), exactMatch: false))
        {
            logger.LogInformation("Final event before disposal");
        }

        await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var reader = new StreamReader(stream))
        {
            Assert.Contains("Final event before disposal", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }
        File.Delete(filePath);
        Assert.False(File.Exists(filePath));
    }


    [Fact]
    public async Task DefensiveGuard_ExpectedInfoIncludedInLogs()
    {
        string directory = CreateLogDirectory();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        var config = new LogSettings
        {
            Target = LogTarget.File,
            LogDirectory = directory,
            LogLevel = LogLevel.Information
        };
        (ILogger logger, string? filePath) = builder.AddLogging(config);
        Assert.NotNull(filePath);

        using (builder.Build())
        {
            try
            {
                throw new DefensiveGuardException("Logging Tests", "unexpected value", "justification");
            }
            catch (DefensiveGuardException ex)
            {
                logger.LogError(ex, "Caught DefensiveGuardException");
            }
        }

        string line = Assert.Single(await File.ReadAllLinesAsync(filePath, TestContext.Current.CancellationToken));
        using (var document = JsonDocument.Parse(line))
        {
            JsonElement root = document.RootElement;
            Assert.Equal("Caught DefensiveGuardException", root.GetProperty("MessageTemplate").GetString());
            Assert.Equal("Error", root.GetProperty("Level").GetString());
            string exception = root.GetProperty("Exception").GetString()!;
            Assert.StartsWith(typeof(DefensiveGuardException).FullName!, exception);
            Assert.Contains("'unexpected value'", exception);
            Assert.Contains("'Logging Tests'", exception);
            Assert.Contains("LoggingRegistrationFileTests.cs:125 in DefensiveGuard_ExpectedInfoIncludedInLogs", exception);
            Assert.Contains(DefensiveGuardException.RepositoryUrl, exception);
        }
        File.Delete(filePath);
        Assert.False(File.Exists(filePath));
    }

    private string CreateLogDirectory()
    {
        string path = Fixture.CombinePath(Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
