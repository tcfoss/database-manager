using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.MsSql.Hosting;

public class MsStartup : Startup<MsConfig>
{
    protected override ConfigurationBuilder<MsConfig> ConfigurationBuilder { get; } = new MsConfigurationBuilder();

    protected override void RegisterServices(IHostApplicationBuilder builder, MsConfig config)
    {
        builder.Services.RegisterMsSqlServices(config);
    }
}