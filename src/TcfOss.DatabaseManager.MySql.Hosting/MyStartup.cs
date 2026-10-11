using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.Hosting;

public class MyStartup : Startup<MyConfig>
{
    protected override ConfigurationBuilder<MyConfig> ConfigurationBuilder { get; } = new MyConfigurationBuilder();

    protected override void RegisterServices(IHostApplicationBuilder builder, MyConfig config)
    {
        builder.Services.RegisterMySqlServices(config);
    }
}
