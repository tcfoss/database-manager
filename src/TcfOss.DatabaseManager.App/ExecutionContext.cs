using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.App;

public sealed class ExecutionContext
{
    public required TextWriter Out { get; init; }
    public required string WorkingDirectory { get; init; }
    public required ILogger Logger { get; init; }
    public required ConfigBase Config { get; init; }
    public required IRunCommands CommandRunner { get; init; }
}
