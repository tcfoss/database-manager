using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.Hosting;

namespace TcfOss.DatabaseManager.MariaDb.Hosting;

public class MariaDbStartResult : IStartResult<MyConfig, MyAppServiceProvider>
{
    public required MyConfig Config { get; init; }
    public required MyAppServiceProvider ServiceProvider { get; init; }
    public required IServiceProvider Services { get; init; }
    public bool UsingDefaultConfig { get; init; }
}
