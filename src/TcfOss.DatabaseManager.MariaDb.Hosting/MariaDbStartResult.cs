using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.Hosting;

namespace TcfOss.DatabaseManager.MariaDb.Hosting;

public record MariaDbStartResult(IHost host) : StartResultBase(host), IStartResult<MyConfig, MyAppServiceProvider>
{
    public required MyConfig Config { get; init; }
    public required MyAppServiceProvider ServiceProvider { get; init; }
    public bool UsingDefaultConfig { get; init; }
}
