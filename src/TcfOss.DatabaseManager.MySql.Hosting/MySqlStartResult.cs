using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.Hosting;

public record MySqlStartResult(IHost host) : StartResultBase(host), IStartResult<MyConfig, MyAppServiceProvider>
{
    public required MyConfig Config { get; init; }
    public required MyAppServiceProvider ServiceProvider { get; init; }
    public bool UsingDefaultConfig { get; init; }
}
