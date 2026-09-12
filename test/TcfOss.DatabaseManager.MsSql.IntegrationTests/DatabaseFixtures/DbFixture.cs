using System.Data.Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MsSql.IntegrationTests.DatabaseFixtures;

public abstract class DbFixture<TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : DbContainerFixture<TBuilderEntity, TContainerEntity>(messageSink)
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    public override DbProviderFactory DbProviderFactory => SqlClientFactory.Instance;
}
