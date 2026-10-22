using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;

namespace TcfOss.DatabaseManager.App;

public sealed class ExecutionContext(IHost host) : IDisposable
{
    public required TextWriter Out { get; init; }
    public required string WorkingDirectory { get; init; }
    public required ILogger Logger { get; init; }
    public required ConfigBase Config { get; init; }
    public required SourceManager SourceManager { get; init; }
    public required IRunCommands CommandRunner { get; init; }

    public void Dispose()
    {
        host.Dispose();
    }
}
