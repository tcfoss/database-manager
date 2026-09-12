using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;

public abstract class DownloadSchemaFixture<TBuilderEntity, TContainerEntity> : IAsyncLifetime
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    protected DbFixture<TBuilderEntity, TContainerEntity> DbFixture { get; init; } = null!;
    protected FsProjectFixture FsProjectFixture { get; init; } = null!;
    public MyConfig MyConfig { get; private set; } = null!;
    public DirectoryInfo RootDirectory => FsProjectFixture.RootDirectory;

    protected abstract SqlDialect Dialect { get; }
    protected virtual bool RemoveSlashesBeforeQuotesGenerationExpression => false;

    public virtual async ValueTask InitializeAsync()
    {
        await DbFixture.Container.StartAsync();
        DbFixture.InitializeLibrarySchema();
        MyConfig = DbFixture.GetLibrarySchemaConfig(RootDirectory.FullName, false, false, Dialect, RemoveSlashesBeforeQuotesGenerationExpression, objectNamePrefixWithSchema: false);

        var schemaDownloader = Core.App.AppServiceProvider.SchemaDownloader;
        await schemaDownloader.ExecuteAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DbFixture.Container.StopAsync();
        await DbFixture.Container.DisposeAsync();
        FsProjectFixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
