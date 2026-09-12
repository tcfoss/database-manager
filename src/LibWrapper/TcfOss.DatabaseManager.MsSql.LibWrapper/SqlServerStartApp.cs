using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.LibWrapper;
using TcfOss.DatabaseManager.MsSql.App;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.MsSql.LibWrapper;

public class SqlServerStartApp : StartAppBase<SqlServerStartResult>
{
    protected override SqlDialect Dialect => SqlDialect.MsSql;

    public override SqlServerStartResult Start(string? configFilePath = null, Config? rawConfig = null, string? workingDirectory = null, bool relaxed = false, StartupOptions? options = null)
    {
        StartResult result = StartCommon(configFilePath, rawConfig, workingDirectory, relaxed, options);

        return new SqlServerStartResult()
        {
            Config = (MsConfig)result.Config,
            ServiceProvider = new AppServiceProvider(),
            Services = result.Services,
            UsingDefaultConfig = result.UsingDefaultConfig
        };
    }

    protected override StartupBase GetStartup()
    {
        return new MsStartup();
    }
}
