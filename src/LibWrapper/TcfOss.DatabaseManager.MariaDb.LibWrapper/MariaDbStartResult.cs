using TcfOss.DatabaseManager.Core.LibWrapper;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MariaDb.LibWrapper;

public class MariaDbStartResult : IStartResult<MyConfig, MyAppServiceProvider>
{
    public required MyConfig Config { get; init; }
    public required MyAppServiceProvider ServiceProvider { get; init; }
    public required IServiceProvider Services { get; init; }
    public bool UsingDefaultConfig { get; init; }
}
