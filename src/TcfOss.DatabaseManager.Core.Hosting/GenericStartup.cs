using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.Core.Hosting;

public class GenericStartup : StartupBase<GenericConfig>
{
    protected override ConfigurationBuilder<GenericConfig> ConfigurationBuilder { get; } = new GenericConfigurationBuilder();

    protected override void RegisterServices(IHostApplicationBuilder builder, GenericConfig config)
    {
        builder.Services.RegisterGenericServices(config);
    }
}
