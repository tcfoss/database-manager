using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.LibWrapper;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.LibWrapper;

public class MySqlStartApp : StartAppBase<MySqlStartResult>
{
    protected override SqlDialect Dialect => SqlDialect.MySql;

    public override MySqlStartResult Start(string? configFilePath = null, Config? rawConfig = null, string? workingDirectory = null, bool relaxed = false, StartupOptions? options = null)
    {
        StartResult result = StartCommon(configFilePath, rawConfig, workingDirectory, relaxed, options);

        return new MySqlStartResult()
        {
            Config = (MyConfig)result.Config,
            ServiceProvider = new MyAppServiceProvider(),
            Services = result.Services,
            UsingDefaultConfig = result.UsingDefaultConfig
        };
    }

    protected override StartupBase GetStartup()
    {
        return new MyStartup();
    }
}
