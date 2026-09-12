using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;
using TcfOss.DatabaseManager.MySql.Tests.Configuration;

namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding;

public class DefinitionHelpersTests
{
    private static MyDefinition BuildDefinition(string sql)
    {
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig(
            dialect: SqlDialect.MySql,
            projectDirectory: "/fake/project/root");
        var logger = new LoggerFactory().CreateLogger<MyDefinitionBuilder>();
        var otherInfo = MyGetOtherData.GetData(rawConfig);
        var config = new MyConfigLoader(logger).LoadConfig("/home/username/database", rawConfig, otherInfo, relaxed: false);
        var definitionBuilder = new MyDefinitionBuilder(config, new SourceManager(), new MyFunctionNameProvider(), logger);
        var textParser = new TextParser(new MyLexer(), new MyParser());

        var statements = textParser.ParseText(sql);
        definitionBuilder.ProcessStatements(statements, config.Schemas.Keys.First(), 0);

        return definitionBuilder.ToDefinition();
    }

    private static MyDefinition BuildUnsortedDefinition()
    {
        var statementText = """
		CREATE TABLE `schema1`.`z_parent` (
			`id` INT UNSIGNED NOT NULL,
			PRIMARY KEY (`id`)
		);

		CREATE TABLE `schema1`.`a_child` (
			`id` INT UNSIGNED NOT NULL,
			`parent_id1` INT UNSIGNED NOT NULL,
			`parent_id2` INT UNSIGNED NOT NULL,
			`code` VARCHAR(100) NOT NULL,
			PRIMARY KEY (`id`),
			KEY `idx_z_code` (`code`),
			KEY `idx_a_parent` (`parent_id1`),
			CONSTRAINT `uq_z_code` UNIQUE KEY (`code`),
			CONSTRAINT `uq_a_parent` UNIQUE KEY (`parent_id2`),
			CONSTRAINT `fk_z_parent2` FOREIGN KEY (`parent_id2`) REFERENCES `schema1`.`z_parent` (`id`),
			CONSTRAINT `fk_a_parent1` FOREIGN KEY (`parent_id1`) REFERENCES `schema1`.`z_parent` (`id`),
			CONSTRAINT `ck_z_parent2` CHECK (`parent_id2` > 0),
			CONSTRAINT `ck_a_parent1` CHECK (`parent_id1` > 0)
		);

		CREATE DEFINER = `test_user`@`test_host` SQL SECURITY DEFINER VIEW `schema1`.`z_view` AS
			SELECT `id`
			FROM `schema1`.`a_child`;

		CREATE DEFINER = `test_user`@`test_host` SQL SECURITY DEFINER VIEW `schema1`.`a_view` AS
			SELECT `id`
			FROM `schema1`.`a_child`;

		CREATE DEFINER = `test_user`@`test_host` PROCEDURE `schema1`.`z_proc`()
			SQL SECURITY DEFINER
			SELECT 1;

		CREATE DEFINER = `test_user`@`test_host` PROCEDURE `schema1`.`a_proc`()
			SQL SECURITY DEFINER
			SELECT 1;

		CREATE DEFINER = `test_user`@`test_host` FUNCTION `schema1`.`z_func`()
			RETURNS INT
			SQL SECURITY DEFINER
		RETURN 1;

		CREATE DEFINER = `test_user`@`test_host` FUNCTION `schema1`.`a_func`()
			RETURNS INT
			SQL SECURITY DEFINER
		RETURN 1;

		CREATE DEFINER = `test_user`@`test_host` TRIGGER `schema1`.`z_trigger`
			BEFORE INSERT ON `schema1`.`a_child`
			FOR EACH ROW
			SET NEW.`code` = NEW.`code`;

		CREATE DEFINER = `test_user`@`test_host` TRIGGER `schema1`.`a_trigger`
			BEFORE UPDATE ON `schema1`.`a_child`
			FOR EACH ROW
			SET NEW.`code` = NEW.`code`;

		CREATE DEFINER = `test_user`@`test_host` EVENT `schema1`.`z_event`
			ON SCHEDULE EVERY 1 DAY
			DO
				SET @a = 1;

		CREATE DEFINER = `test_user`@`test_host` EVENT `schema1`.`a_event`
			ON SCHEDULE EVERY 1 DAY
			DO
				SET @a = 1;
		""";

        return BuildDefinition(statementText);
    }

    [Fact]
    public void SortDefinition_SortsTopLevelObjectsByHandle()
    {
        var unsorted = BuildUnsortedDefinition();

        var sorted = DefinitionHelpers.SortDefinition(unsorted);

        Assert.Equal(["a_child", "z_parent"], [.. sorted.Tables.Keys.Select(k => k.Name)]);
        Assert.Equal(["a_view", "z_view"], [.. sorted.Views.Keys.Select(k => k.Name)]);
        Assert.Equal(["a_proc", "z_proc"], [.. sorted.Procedures.Keys.Select(k => k.Name)]);
        Assert.Equal(["a_func", "z_func"], [.. sorted.Functions.Keys.Select(k => k.Name)]);
        Assert.Equal(["a_trigger", "z_trigger"], [.. sorted.Triggers.Keys.Select(k => k.Name)]);
        Assert.Equal(["a_event", "z_event"], [.. sorted.Events.Keys.Select(k => k.Name)]);
    }

    [Fact]
    public void SortDefinition_SortsTableConstraintCollectionsByName()
    {
        var unsorted = BuildUnsortedDefinition();

        var sorted = DefinitionHelpers.SortDefinition(unsorted);
        var childTable = sorted.Tables.Values.Single(t => t.Name.Name == "a_child");

        Assert.Equal(["idx_a_parent", "idx_z_code"], [.. childTable.Keys.Keys.Select(k => k.Name)]);
        Assert.Equal(["uq_a_parent", "uq_z_code"], [.. childTable.UniqueKeys.Keys.Select(k => k.Name)]);
        Assert.Equal(["fk_a_parent1", "fk_z_parent2"], [.. childTable.ForeignKeys.Keys.Select(k => k.Name)]);
        Assert.Equal(["ck_a_parent1", "ck_z_parent2"], [.. childTable.Checks.Keys.Select(k => k.Name)]);
    }
}
