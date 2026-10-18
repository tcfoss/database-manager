using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TcfOss.DatabaseManager.Core.Tests;

public sealed class TestLogger : ILogger
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), eventId, exception));
    }

    public sealed record LogEntry(LogLevel Level, string Message, EventId EventId, Exception? Exception);
}
