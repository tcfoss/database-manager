using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.Hosting;

public record MySqlStartResult : IStartResult<MyConfig, MyAppServiceProvider>
{
    public required MyConfig Config { get; init; }
    public required MyAppServiceProvider ServiceProvider { get; init; }
    public required IServiceProvider Services { get; init; }
    public bool UsingDefaultConfig { get; init; }
}
