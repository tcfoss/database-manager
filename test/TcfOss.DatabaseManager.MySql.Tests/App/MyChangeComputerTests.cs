using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.Tests.App;

public class MyChangeComputerTests
{
    [Fact]
    public async Task ExecuteAsync_UsesInjectedLoadersConcurrently()
    {
        var databaseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var filesystemStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        CancellationToken cancellationToken = cancellation.Token;
        bool? filesystemRelaxed = null;
        var metadata = new StubMetadataLoader();
        var dbLoader = new StubDbLoader(async receivedToken =>
        {
            Assert.Equal(cancellationToken, receivedToken);
            databaseStarted.SetResult();
            await filesystemStarted.Task.WaitAsync(receivedToken);
            return new MyDefinition();
        });
        var fsLoader = new StubFsLoader(relaxed =>
        {
            filesystemRelaxed = relaxed;
            Assert.True(databaseStarted.Task.IsCompleted);
            filesystemStarted.SetResult();
            return new MyDefinition();
        });
        var computer = CreateComputer(dbLoader, fsLoader, metadata);

        await computer.ExecuteAsync(null, cancellationToken: cancellationToken);

        Assert.True(filesystemStarted.Task.IsCompleted);
        Assert.False(filesystemRelaxed ?? true);
        Assert.Equal(2, metadata.ReadCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeStart_DoesNotInvokeLoaders()
    {
        var dbLoader = new StubDbLoader(_ => throw new InvalidOperationException("Database loader must not run."));
        var fsLoader = new StubFsLoader(_ => throw new InvalidOperationException("Filesystem loader must not run."));
        var metadata = new StubMetadataLoader();
        var computer = CreateComputer(dbLoader, fsLoader, metadata);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            computer.ExecuteAsync(null, cancellationToken: cancellation.Token));

        Assert.Equal(0, metadata.ReadCount);
    }

    [Fact]
    public async Task ExecuteAsync_DatabaseLoadFails_DoesNotReadMetadata()
    {
        var failure = new InvalidOperationException("Database loading failed.");
        var dbLoader = new StubDbLoader(_ => Task.FromException<MyDefinition>(failure));
        var fsLoader = new StubFsLoader(_ => new MyDefinition());
        var metadata = new StubMetadataLoader();
        var computer = CreateComputer(dbLoader, fsLoader, metadata);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            computer.ExecuteAsync(null, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(failure, actual);
        Assert.Equal(0, metadata.ReadCount);
    }

    [Fact]
    public async Task ExecuteAsync_RepeatedCalls_ReuseInjectedLoaders()
    {
        var databaseLoads = 0;
        var filesystemLoads = 0;
        var dbLoader = new StubDbLoader(_ =>
        {
            databaseLoads++;
            return Task.FromResult(new MyDefinition());
        });
        var fsLoader = new StubFsLoader(_ =>
        {
            filesystemLoads++;
            return new MyDefinition();
        });
        var metadata = new StubMetadataLoader();
        var computer = CreateComputer(dbLoader, fsLoader, metadata);

        await computer.ExecuteAsync(null, cancellationToken: TestContext.Current.CancellationToken);
        await computer.ExecuteAsync(null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, databaseLoads);
        Assert.Equal(2, filesystemLoads);
        Assert.Equal(4, metadata.ReadCount);
    }

    private static MyChangeComputer CreateComputer(
        ILoadDbDefinition<MyDefinition> dbLoader,
        ILoadFsDefinition<MyDefinition> fsLoader,
        StubMetadataLoader metadata)
    {
        return new MyChangeComputer(
            TestConfig.GetMyTestConfig(),
            new FileWriter(NullLogger<FileWriter>.Instance),
            dbLoader,
            fsLoader,
            metadata,
            metadata,
            NullLogger<MyChangeComputer>.Instance);
    }

    private sealed class StubDbLoader(Func<CancellationToken, Task<MyDefinition>> loadDefinition)
        : ILoadDbDefinition<MyDefinition>
    {
        public Task<MyDefinition> LoadDefinitionAsync(CancellationToken cancellationToken = default)
        {
            return loadDefinition(cancellationToken);
        }
    }

    private sealed class StubFsLoader(Func<bool, MyDefinition> loadDefinition)
        : ILoadFsDefinition<MyDefinition>
    {
        public MyDefinition LoadDefinition(bool relaxed)
        {
            return loadDefinition(relaxed);
        }
    }

    private sealed class StubMetadataLoader : IGetUnappliedRefactors, IGetUnappliedDeployScripts
    {
        public int ReadCount { get; private set; }

        public Task<IEnumerable<string>> GetUnappliedRefactorIdsAsync(
            SchemaIdentifier schemaId, IReadOnlyCollection<string> allRefactorIds)
        {
            return Task.FromResult<IEnumerable<string>>([]);
        }

        public Task<List<Refactor>> GetAllUnappliedRefactorsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds)
        {
            ReadCount++;
            return Task.FromResult<List<Refactor>>([]);
        }

        public Task<List<DeployScript>> GetAllUnappliedDeployScriptsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds)
        {
            ReadCount++;
            return Task.FromResult<List<DeployScript>>([]);
        }
    }
}
