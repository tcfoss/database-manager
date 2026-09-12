using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;

namespace TcfOss.DatabaseManager.App.IntegrationTests;

public abstract class CliReadOnlyFixture : IAsyncLifetime
{
    public abstract IDatabaseContainer Container { get; }
    public abstract ValueTask InitializeAsync();
    public abstract ValueTask DisposeAsync();
}

public abstract class CliReadOnlyFixture<TBuilderEntity, TContainerEntity>
    : CliReadOnlyFixture
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    protected DbFixture<TBuilderEntity, TContainerEntity> DbFixture { get; init; } = null!;
    public override IDatabaseContainer Container => DbFixture.Container;
    private FsProjectFixture InitialFsFixture { get; } = new();

    public override async ValueTask InitializeAsync()
    {
        await DbFixture.Container.StartAsync();
        await DbFixture.Container.InitializeLibrarySchema();

        InitialFsFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("Initial").FullName);
    }

    public override async ValueTask DisposeAsync()
    {
        await DbFixture.Container.StopAsync();
        InitialFsFixture.Dispose();

        GC.SuppressFinalize(this);
    }
}
