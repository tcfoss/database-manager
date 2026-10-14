using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.Core.Hosting;

public record GenericStartResult : IStartResult<GenericConfig, AppServiceProvider>
{
    public required GenericConfig Config { get; init; }
    public required AppServiceProvider ServiceProvider { get; init; }
    public required IServiceProvider Services { get; init; }
    public required bool UsingDefaultConfig { get; init; }
}
