using System.Data.Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using MySqlConnector;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;

public abstract class DbFixture<TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : DbContainerFixture<TBuilderEntity, TContainerEntity>(messageSink)
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    protected abstract ushort Port { get; }

    public override DbProviderFactory DbProviderFactory => MySqlConnectorFactory.Instance;

    public void InitializeLibrarySchema()
    {
        Container.ExecAsync(["sh", "/InitialSchemas/Init/initialize_library.sh"]).GetAwaiter().GetResult();
    }

    public MyConfig GetLibrarySchemaConfig(string rootPath, bool includeScripts, bool includeRefactors, SqlDialect dialect = SqlDialect.MariaDb, bool removeSlashesBeforeQuotesGenerationExpression = false, bool objectNamePrefixWithSchema = true)
    {
        return Container.GetLibrarySchemaConfig(rootPath, includeScripts, includeRefactors, Port, dialect, removeSlashesBeforeQuotesGenerationExpression, objectNamePrefixWithSchema: objectNamePrefixWithSchema);
    }

    public MyConfig GetSimpleSchemaConfig(string rootPath, SqlDialect dialect)
    {
        return Container.GetSimpleSchemaConfig(rootPath, Port, dialect);
    }
}
