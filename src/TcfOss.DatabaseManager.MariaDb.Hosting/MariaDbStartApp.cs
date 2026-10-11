using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.Hosting;

namespace TcfOss.DatabaseManager.MariaDb.Hosting;

public class MariaDbStartApp : StartAppBase<MariaDbStartResult>
{
    protected override SqlDialect Dialect => SqlDialect.MySql;

    public override MariaDbStartResult Start(string? configFilePath = null, Config? rawConfig = null, string? workingDirectory = null, bool relaxed = false, StartupOptions? options = null)
    {
        StartResult result = StartCommon(configFilePath, rawConfig, workingDirectory, relaxed, options);

        return new MariaDbStartResult()
        {
            Config = (MyConfig)result.Config,
            ServiceProvider = new MyAppServiceProvider(),
            Services = result.Services,
            UsingDefaultConfig = result.UsingDefaultConfig
        };
    }

    protected override StartupBase GetStartup()
    {
        return new MaStartup();
    }
}
