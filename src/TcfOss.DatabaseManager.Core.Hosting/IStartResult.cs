using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.Core.Hosting;

public interface IStartResult<out TConfig, out TServiceProvider> : IDisposable, IAsyncDisposable
    where TConfig : ConfigBase
    where TServiceProvider : AppServiceProvider
{
    public TConfig Config { get; }
    public TServiceProvider ServiceProvider { get; }
    public IServiceProvider Services { get; }
    public bool UsingDefaultConfig { get; }
}
