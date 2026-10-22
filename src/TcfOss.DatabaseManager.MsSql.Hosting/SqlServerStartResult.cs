using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.MsSql.Hosting;

public record SqlServerStartResult(IHost host) : StartResultBase(host), IStartResult<MsConfig, AppServiceProvider>
{
    public required MsConfig Config { get; init; }
    public required AppServiceProvider ServiceProvider { get; init; }
    public required bool UsingDefaultConfig { get; init; }
}
