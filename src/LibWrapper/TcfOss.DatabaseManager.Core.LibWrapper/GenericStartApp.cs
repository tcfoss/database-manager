using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.LibWrapper;

public class GenericStartApp : StartAppBase<GenericStartResult>
{
    protected override SqlDialect Dialect => SqlDialect.Generic;

    public override GenericStartResult Start(string? configFilePath = null, Config? rawConfig = null, string? workingDirectory = null, bool relaxed = false, StartupOptions? options = null)
    {
        StartResult result = StartCommon(configFilePath, rawConfig, workingDirectory, relaxed, options);

        return new GenericStartResult()
        {
            Config = (ConfigGeneric)result.Config,
            ServiceProvider = new AppServiceProvider(),
            Services = result.Services,
            UsingDefaultConfig = result.UsingDefaultConfig
        };
    }

    protected override StartupBase GetStartup()
    {
        return new GenericStartup();
    }
}
