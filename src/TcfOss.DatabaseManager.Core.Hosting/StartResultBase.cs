using Microsoft.Extensions.Hosting;

namespace TcfOss.DatabaseManager.Core.Hosting;

public abstract record StartResultBase(IHost host) : IDisposable, IAsyncDisposable
{
    public IServiceProvider Services { get; } = host.Services;

    public void Dispose()
    {
        host.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (host is IAsyncDisposable asyncHost)
        {
            await asyncHost.DisposeAsync();
        }
        else
        {
            host.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
