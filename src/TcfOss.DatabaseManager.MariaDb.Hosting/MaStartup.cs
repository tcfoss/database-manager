using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MariaDb.Configuration;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MariaDb.Hosting;

public class MaStartup : StartupBase<MyConfig>
{
    protected override ConfigurationBuilder<MyConfig> ConfigurationBuilder { get; } = new MaConfigurationBuilder();

    protected override void RegisterServices(IHostApplicationBuilder builder, MyConfig config)
    {
        builder.Services.RegisterMariaDbServices(config);
    }
}
