using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Hosting;

public class GenericStartApp : StartAppBase<GenericStartResult>
{
    protected override SqlDialect Dialect => SqlDialect.Generic;

    public override GenericStartResult Start(string? configFilePath = null, Config? rawConfig = null, string? workingDirectory = null, bool relaxed = false, StartupOptions? options = null)
    {
        StartResult result = StartCommon(configFilePath, rawConfig, workingDirectory, relaxed, options);

        return new GenericStartResult(result.Host)
        {
            Config = (GenericConfig)result.Config,
            ServiceProvider = new AppServiceProvider(),
            UsingDefaultConfig = result.UsingDefaultConfig
        };
    }

    protected override StartupBase GetStartup()
    {
        return new GenericStartup();
    }
}
