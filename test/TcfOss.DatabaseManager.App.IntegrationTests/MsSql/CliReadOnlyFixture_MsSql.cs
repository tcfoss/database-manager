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
        try
        {
            await DbFixture.Container.StartAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Console.WriteLine($"Container id: {DbFixture.Container.Id}");

            try
            {
                var (stdout, stderr) = await DbFixture.Container.GetLogsAsync();
                Console.WriteLine("Container stdout:");
                Console.WriteLine(stdout);

                Console.WriteLine("Container stderr:");
                Console.WriteLine(stderr);
            }
            catch (Exception logException)
            {
                Console.WriteLine("Failed to get container logs:");
                Console.WriteLine(logException);
            }
            throw;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await DbFixture.Container.StopAsync();
        GC.SuppressFinalize(this);
    }
}
