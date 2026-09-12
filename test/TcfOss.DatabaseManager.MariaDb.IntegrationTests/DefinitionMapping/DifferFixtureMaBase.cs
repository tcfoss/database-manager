using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

public abstract class DifferFixtureMaBase<TBuilderEntity, TContainerEntity>
    : DifferFixture<TBuilderEntity, TContainerEntity>
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;
}
