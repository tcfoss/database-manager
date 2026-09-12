using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TcfOss.DatabaseManager.Core.LibWrapper;

public sealed class StartupOptions
{
    public Action<IHostApplicationBuilder>? ConfigureBuilder { get; init; }

    public Action<IServiceCollection>? ConfigureServices { get; init; }
}
