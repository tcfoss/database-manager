using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.Core.LibWrapper;

public interface IStartResult<out TConfig, out TServiceProvider>
    where TConfig : ConfigBase
    where TServiceProvider : AppServiceProvider
{
    public TConfig Config { get; }
    public TServiceProvider ServiceProvider { get; }
    public IServiceProvider Services { get; }
    public bool UsingDefaultConfig { get; }
}
