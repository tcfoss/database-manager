using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.Core.LibWrapper;

public record GenericStartResult : IStartResult<ConfigGeneric, AppServiceProvider>
{
    public required ConfigGeneric Config { get; init; }
    public required AppServiceProvider ServiceProvider { get; init; }
    public required IServiceProvider Services { get; init; }
    public required bool UsingDefaultConfig { get; init; }
}
