using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.Core.Hosting;

public class GenericStartup : Startup<ConfigGeneric>
{
    protected override ConfigurationBuilder<ConfigGeneric> ConfigurationBuilder { get; } = new GenericConfigurationBuilder();

    protected override void RegisterServices(IHostApplicationBuilder builder, ConfigGeneric config)
    {
        builder.Services.RegisterGenericServices(config);
    }
}
