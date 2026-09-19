using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

public abstract class SimpleSchemaDifferTests<TBuilderEntity, TContainerEntity, TFixture> : IClassFixture<TFixture>
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
    where TFixture : SimpleSchemaDifferFixture<TBuilderEntity, TContainerEntity>
{
    protected SimpleSchemaDifferFixture<TBuilderEntity, TContainerEntity> Fixture { get; init; } = null!;

    [Fact]
    public void Table_Default_And_Explicit_Column_Changes_Are_Detected()
    {
        var actualStatements = Fixture.ActualChanges
            .OrderBy(change => change.Weight)
            .ThenBy(change => change.Statement.ToSql())
            .Select(change => change.Statement.ToSql())
            .ToArray();

        List<string> expectedStatements =
        [
            "ALTER TABLE `samples` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci",
            "ALTER TABLE `widgets` DEFAULT CHARACTER SET latin1 COLLATE latin1_swedish_ci",
            "ALTER TABLE `nulltest` MODIFY COLUMN `name` VARCHAR(50) NULL",
            "ALTER TABLE `samples` MODIFY COLUMN `changed_collation` VARCHAR(100) NOT NULL, MODIFY COLUMN `changed_charset` CHAR(10) NOT NULL",
            "UPDATE `nulltest` SET `name` = 'Probably Beta, but more research is needed' WHERE `name` IS NULL;\n",
            "INSERT INTO `simple_schema`.`_database_manager` (`entry_key`, `entry_type`) VALUES ('00000000-0000-0000-0000-000000000001', 'D')",
            "ALTER TABLE `nulltest` MODIFY COLUMN `name` VARCHAR(50) NOT NULL",
        ];

        Assert.Equal(expectedStatements, actualStatements);
    }
}
