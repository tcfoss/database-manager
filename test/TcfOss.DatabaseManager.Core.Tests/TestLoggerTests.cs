using Microsoft.Extensions.Logging;

namespace TcfOss.DatabaseManager.Core.Tests;

public class TestLoggerTests
{
    [Fact]
    public void Log_ConcurrentWrites_PreservesEveryEntry()
    {
        var logger = new TestLogger();
        var exception = new InvalidOperationException("Test exception");
        const int entryCount = 1000;

        Parallel.For(0, entryCount, index =>
        {
            logger.Log(LogLevel.Information, new EventId(index, "Concurrent"), index, exception,
                static (state, _) => $"Message {state}");
        });

        Assert.Equal(entryCount, logger.Entries.Count);
        Assert.Equal(entryCount, logger.Entries.Select(entry => entry.EventId.Id).Distinct().Count());
        Assert.All(logger.Entries, entry =>
        {
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal("Concurrent", entry.EventId.Name);
            Assert.InRange(entry.EventId.Id, 0, entryCount - 1);
            Assert.Equal($"Message {entry.EventId.Id}", entry.Message);
            Assert.Same(exception, entry.Exception);
        });
    }
}