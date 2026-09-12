using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.LibWrapper;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.MsSql.LibWrapper;

public record SqlServerStartResult : IStartResult<MsConfig, AppServiceProvider>
{
    public required MsConfig Config { get; init; }
    public required AppServiceProvider ServiceProvider { get; init; }
    public required IServiceProvider Services { get; init; }
    public required bool UsingDefaultConfig { get; init; }
}
