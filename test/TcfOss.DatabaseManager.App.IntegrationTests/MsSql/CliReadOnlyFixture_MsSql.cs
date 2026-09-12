using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.MsSql.IntegrationTests.DatabaseFixtures;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

public abstract class CliReadOnlyFixture_MsSql : IAsyncLifetime
{
    public abstract ValueTask InitializeAsync();
    public abstract ValueTask DisposeAsync();
}

public abstract class CliReadOnlyFixture_MsSql<TBuilderEntity, TContainerEntity>
    : CliReadOnlyFixture_MsSql
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    protected DbFixture<TBuilderEntity, TContainerEntity> DbFixture { get; init; } = null!;

    public override async ValueTask InitializeAsync()
    {
        await DbFixture.Container.StartAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await DbFixture.Container.StopAsync();
        GC.SuppressFinalize(this);
    }
}
