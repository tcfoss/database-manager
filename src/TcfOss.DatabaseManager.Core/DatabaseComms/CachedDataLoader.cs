using System.Collections.Concurrent;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DatabaseComms;

public sealed class CachedDataLoader<T>(Func<InfoSchemaKey, CancellationToken, Task<T>> loader) : IDisposable
{
    private readonly ConcurrentDictionary<InfoSchemaKey, Lazy<Task<T>>> _data = new();
    private readonly CancellationTokenSource _disposeCts = new();

    public async Task<T> GetAsync(InfoSchemaKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Lazy<Task<T>> lazyTask = _data.GetOrAdd(key, CreateLazy);

        try
        {
            return await lazyTask.Value.WaitAsync(cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            // The fetch failed or was canceled; don't cache the results.
            _ = _data.TryRemove(new KeyValuePair<InfoSchemaKey, Lazy<Task<T>>>(key, lazyTask));
            throw;
        }
    }

    private Lazy<Task<T>> CreateLazy(InfoSchemaKey key)
    {
        return new Lazy<Task<T>>(() => loader(key, _disposeCts.Token), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public void Clear(InfoSchemaKey key)
    {
        _ = _data.TryRemove(key, out _);
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
