using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseComms;

namespace TcfOss.DatabaseManager.Core.Tests.DatabaseComms;

public class CachedDataLoaderTests
{
    [Fact]
    public async Task GetAsync_WhenCancellationTokenIsAlreadyCanceled_DoesNotStartLoad()
    {
        int loadCount = 0;
        var loader = new CachedDataLoader<int>((_, _) =>
        {
            loadCount++;
            return Task.FromResult(42);
        });
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => loader.GetAsync(new InfoSchemaKey("catalog", "schema"), cancellationTokenSource.Token));

        Assert.Equal(0, loadCount);
    }

    [Fact]
    public async Task GetAsync_ConcurrentCallers_ShareOneLoad()
    {
        int loadCount = 0;
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var loader = new CachedDataLoader<int>((_, _) =>
        {
            Interlocked.Increment(ref loadCount);
            return gate.Task;
        });
        var key = new InfoSchemaKey("catalog", "schema");

        Task<int> first = loader.GetAsync(key, TestContext.Current.CancellationToken);
        Task<int> second = loader.GetAsync(key, TestContext.Current.CancellationToken);
        gate.SetResult(7);

        int[] results = await Task.WhenAll(first, second);
        Assert.Equal(new[] { 7, 7 }, results);
        Assert.Equal(1, loadCount);
    }

    [Fact]
    public async Task Clear_ForcesReload()
    {
        int loadCount = 0;
        using var loader = new CachedDataLoader<int>((_, _) => Task.FromResult(++loadCount));
        var key = new InfoSchemaKey("catalog", "schema");

        Assert.Equal(1, await loader.GetAsync(key, TestContext.Current.CancellationToken));
        Assert.Equal(1, await loader.GetAsync(key, TestContext.Current.CancellationToken));
        loader.Clear(key);
        Assert.Equal(2, await loader.GetAsync(key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_FailedLoad_IsNotCached()
    {
        int loadCount = 0;
        using var loader = new CachedDataLoader<int>((_, _) =>
            ++loadCount == 1 ? Task.FromException<int>(new InvalidOperationException()) : Task.FromResult(42));
        var key = new InfoSchemaKey("catalog", "schema");

        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.GetAsync(key, TestContext.Current.CancellationToken));
        Assert.Equal(42, await loader.GetAsync(key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_CallerCancels_DoesNotCancelSharedLoad()
    {
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var loader = new CachedDataLoader<int>((_, _) => gate.Task);
        var key = new InfoSchemaKey("catalog", "schema");
        using var callerCts = new CancellationTokenSource();

        Task<int> canceledCaller = loader.GetAsync(key, callerCts.Token);
        Task<int> otherCaller = loader.GetAsync(key, TestContext.Current.CancellationToken);
        await callerCts.CancelAsync();
        gate.SetResult(5);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledCaller);
        Assert.Equal(5, await otherCaller);
    }

    [Fact]
    public async Task Dispose_CancelsInFlightLoad()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new CachedDataLoader<int>(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        });

        Task<int> pending = loader.GetAsync(new InfoSchemaKey("catalog", "schema"), TestContext.Current.CancellationToken);
        await started.Task;
        loader.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
