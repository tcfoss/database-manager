using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration;

// YAML parsing target
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
public class LogSettings
{
    public LogTarget Target { get; init; } = LogTarget.Console;
    public string LogDirectory { get; init; } = Path.GetTempPath();
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public LogLevel DatabaseLogLevel { get; init; } = LogLevel.Warning;
    public bool EnableSensitiveDataLogging { get; init; }
}
