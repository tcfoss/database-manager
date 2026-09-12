using System.Collections;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;
using TcfOss.DatabaseManager.MySql.Tests.Configuration;
using Xunit.Sdk;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding.MyDefinitionBuilderTestsBase.DataTypeSerializer), typeof(DataType))]


namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding;

public class MyDefinitionBuilderTests : MyDefinitionBuilderTestsBase
{
    protected override bool Relaxed => false;
    protected override MyConfig MyConfig { get; }
    protected override MyDefinitionBuilder DefinitionBuilder { get; }
    protected virtual Func<MyConfig, ILogger<MyDefinitionBuilder>, SourceManager, MyDefinitionBuilder> DefinitionBuilderFactory => (config, logger, sourceManager) => new MyDefinitionBuilder(config, sourceManager, new MyFunctionNameProvider(), logger);

    public MyDefinitionBuilderTests()
    {
        // ReSharper disable VirtualMemberCallInConstructor
        var differFormatSettings = new ConfigParsing.DifferFormattingSettings
        {
            OmitModifiersIfDefault = false,
            ViewPreferNormalizedBody = true,
        };
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig(
            dialect: SqlDialect.MySql,
            projectDirectory: "/fake/project/root",
            differFormattingSettings: differFormatSettings);
        var logger = new LoggerFactory().CreateLogger<MyDefinitionBuilder>();
        var otherInfo = MyGetOtherData.GetData(rawConfig);
        MyConfig = new MyConfigLoader(logger).LoadConfig("/home/username/database", rawConfig, otherInfo, Relaxed);
        DefinitionBuilder = GetDefinitionBuilder();
        // ReSharper restore VirtualMemberCallInConstructor
    }

    protected override MyDefinitionBuilder GetDefinitionBuilder()
    {
        var logger = new LoggerFactory().CreateLogger<MyDefinitionBuilder>();
        return DefinitionBuilderFactory(MyConfig, logger, new SourceManager());
    }

    private (MyConfig Config, MyDefinitionBuilder Builder, TextParser Parser) CreateDefinitionBuilderForTest(string? defaultDefinerAccount, string? defaultDefinerHost)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.ProjectDirectory = "/fake/project/root";
        rawConfig.Dialect = SqlDialect.MySql;
        rawConfig.DefaultDefinerAccount = defaultDefinerAccount;
        rawConfig.DefaultDefinerHost = defaultDefinerHost;

        var logger = new LoggerFactory().CreateLogger<MyDefinitionBuilder>();
        var otherInfo = MyGetOtherData.GetData(rawConfig);
        var config = new MyConfigLoader(logger).LoadConfig("/home/username/database", rawConfig, otherInfo, Relaxed);

        var builder = DefinitionBuilderFactory(config, logger, new SourceManager());
        var parser = new TextParser(new MyLexer(), new MyParser());

        return (config, builder, parser);
    }

    [Fact]
    public void Procedure_NoDefiner_Uses_ConfigDefaultDefiner()
    {
        var (config, builder, parser) = CreateDefinitionBuilderForTest("`cfg_user`", "`cfg_host`");
        SchemaIdentifier schema = config.Schemas.Keys.First();

        var statementText = """
        CREATE PROCEDURE `schema1`.`my_procedure`()
            SQL SECURITY DEFINER
        BEGIN
            SELECT 1;
        END;
        """;

        var statements = parser.ParseText(statementText);
        builder.ProcessStatements(statements, schema, 0);

        var def = builder.ToDefinition();
        var procedure = def.Procedures.First(p => p.Key.Name == "my_procedure").Value;

        var expected = new Account.IdentityWithHost(
            new ExtendedIdentifier("cfg_user", ExtendedQuoteStyle.Backticks),
            new ExtendedIdentifier("cfg_host", ExtendedQuoteStyle.Backticks));
        Assert.Equal(expected, procedure.Definer.Account);
    }

    [Fact]
    public void Function_NoDefiner_Uses_ConfigDefaultDefiner()
    {
        var (config, builder, parser) = CreateDefinitionBuilderForTest("`cfg_user`", null);
        SchemaIdentifier schema = config.Schemas.Keys.First();

        var statementText = """
        CREATE FUNCTION `schema1`.`my_function`()
            RETURNS INT
            SQL SECURITY DEFINER
        RETURN 1;
        """;

        var statements = parser.ParseText(statementText);
        builder.ProcessStatements(statements, schema, 0);

        var def = builder.ToDefinition();
        var function = def.Functions.First(f => f.Key.Name == "my_function").Value;

        var expected = new Account.Identity(new ExtendedIdentifier("cfg_user", ExtendedQuoteStyle.Backticks));
        Assert.Equal(expected, function.Definer.Account);
    }

    [Fact]
    public void View_NoDefiner_Uses_ConfigDefaultDefiner()
    {
        var (config, builder, parser) = CreateDefinitionBuilderForTest("`cfg_user`", "`cfg_host`");
        SchemaIdentifier schema = config.Schemas.Keys.First();

        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT NOT NULL,
            PRIMARY KEY (`id`)
        );

        CREATE
            SQL SECURITY DEFINER
        VIEW `schema1`.`my_view`
        AS
            SELECT id
            FROM `schema1`.`table1`;
        """;

        var statements = parser.ParseText(statementText);
        builder.ProcessStatements(statements, schema, 0);

        var def = builder.ToDefinition();
        var view = def.Views.First(v => v.Key.Name == "my_view").Value;

        var expected = new Account.IdentityWithHost(
            new ExtendedIdentifier("cfg_user", ExtendedQuoteStyle.Backticks),
            new ExtendedIdentifier("cfg_host", ExtendedQuoteStyle.Backticks));
        Assert.Equal(expected, view.Definer.Account);
    }

    [Fact]
    public void Event_NoDefiner_Uses_ConfigDefaultDefiner()
    {
        var (config, builder, parser) = CreateDefinitionBuilderForTest("`cfg_user`", "`cfg_host`");
        SchemaIdentifier schema = config.Schemas.Keys.First();

        var statementText = """
        CREATE EVENT `schema1`.`my_event`
            ON SCHEDULE EVERY 1 DAY
            DO
                SET @a = 1;
        """;

        var statements = parser.ParseText(statementText);
        builder.ProcessStatements(statements, schema, 0);

        var def = builder.ToDefinition();
        var evt = def.Events.First(e => e.Key.Name == "my_event").Value;

        var expected = new Account.IdentityWithHost(
            new ExtendedIdentifier("cfg_user", ExtendedQuoteStyle.Backticks),
            new ExtendedIdentifier("cfg_host", ExtendedQuoteStyle.Backticks));
        Assert.Equal(expected, evt.Definer.Account);
    }

    [Fact]
    public void Trigger_NoDefiner_Uses_ConfigDefaultDefiner()
    {
        var (config, builder, parser) = CreateDefinitionBuilderForTest("`cfg_user`", "`cfg_host`");
        SchemaIdentifier schema = config.Schemas.Keys.First();

        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT NOT NULL,
            PRIMARY KEY (`id`)
        );

        CREATE TRIGGER `schema1`.`my_trigger`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.`id` = NEW.`id`;
        END;
        """;

        var statements = parser.ParseText(statementText);
        builder.ProcessStatements(statements, schema, 0);

        var def = builder.ToDefinition();
        var trigger = def.Triggers.First(t => t.Key.Name == "my_trigger").Value;

        var expected = new Account.IdentityWithHost(
            new ExtendedIdentifier("cfg_user", ExtendedQuoteStyle.Backticks),
            new ExtendedIdentifier("cfg_host", ExtendedQuoteStyle.Backticks));
        Assert.Equal(expected, trigger.Definer.Account);
    }

    [Fact]
    public void Procedure_ExplicitDefiner_NotOverriddenByConfigDefault()
    {
        var (config, builder, parser) = CreateDefinitionBuilderForTest("`cfg_user`", "`cfg_host`");
        SchemaIdentifier schema = config.Schemas.Keys.First();

        var statementText = """
        CREATE
            DEFINER = `statement_user`@`statement_host`
        PROCEDURE `schema1`.`my_procedure`()
            SQL SECURITY DEFINER
        BEGIN
            SELECT 1;
        END;
        """;

        var statements = parser.ParseText(statementText);
        builder.ProcessStatements(statements, schema, 0);

        var def = builder.ToDefinition();
        var procedure = def.Procedures.First(p => p.Key.Name == "my_procedure").Value;

        var expected = new Account.IdentityWithHost(
            new ExtendedIdentifier("statement_user", ExtendedQuoteStyle.Backticks),
            new ExtendedIdentifier("statement_host", ExtendedQuoteStyle.Backticks));
        Assert.Equal(expected, procedure.Definer.Account);
    }

    [Fact]
    public void Function_ExplicitDefiner_NotOverriddenByConfigDefault()
    {
        var (config, builder, parser) = CreateDefinitionBuilderForTest("`cfg_user`", "`cfg_host`");
        SchemaIdentifier schema = config.Schemas.Keys.First();

        var statementText = """
        CREATE
            DEFINER = `statement_user`
        FUNCTION `schema1`.`my_function`()
            RETURNS INT
            SQL SECURITY DEFINER
        RETURN 1;
        """;

        var statements = parser.ParseText(statementText);
        builder.ProcessStatements(statements, schema, 0);

        var def = builder.ToDefinition();
        var function = def.Functions.First(f => f.Key.Name == "my_function").Value;

        var expected = new Account.Identity(new ExtendedIdentifier("statement_user", ExtendedQuoteStyle.Backticks));
        Assert.Equal(expected, function.Definer.Account);
    }

    [Theory]
    [ClassData(typeof(MyDefaultsAppliedTestData))]
    public void My_Test_Defaults_Applied_To_Column(string columnDefinition, DataType expectedDataType)
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `my_field` {columnDefinition}
        );
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Tables);
        var table = def.Tables.Values.First();
        var column = Assert.Single(table.Columns);
        Assert.Equal(expectedDataType, column.DataType);
    }

    [Fact]
    public void TableWithOptions()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT AUTO_INCREMENT,
            `name` VARCHAR(100),
            PRIMARY KEY (`id`)
        ) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci AUTO_INCREMENT = 2000 COMMENT = 'My table comment';
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Tables);
        var table = def.Tables.Values.First();
        Assert.Equal("InnoDB", table.Engine);
        Assert.Equal("utf8mb4", table.CharacterSet);
        Assert.Equal("utf8mb4_0900_ai_ci", table.Collation);
        Assert.Equal(2000UL, table.AutoIncrement);
        Assert.NotNull(table.TableComment);
        Assert.Equal("My table comment", table.TableComment.Text);
    }

    private class MyDefaultsAppliedTestData : IEnumerable<TheoryDataRow<string, DataType>>
    {
        public IEnumerator<TheoryDataRow<string, DataType>> GetEnumerator()
        {
            yield return new TheoryDataRow<string, DataType>(
                "INT UNSIGNED",
                new MyDataType.MyInt { Width = null, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "INT UNSIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "INT SIGNED",
                new MyDataType.MyInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "INT SIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "INT",
                new MyDataType.MyInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "INT" };
            yield return new TheoryDataRow<string, DataType>(
                "TINYINT UNSIGNED",
                new MyDataType.MyTinyInt { Width = null, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "TINYINT UNSIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "TINYINT SIGNED",
                new MyDataType.MyTinyInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "TINYINT SIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "TINYINT",
                new MyDataType.MyTinyInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "TINYINT" };
            yield return new TheoryDataRow<string, DataType>(
                "SMALLINT UNSIGNED",
                new MyDataType.MySmallInt { Width = null, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "SMALLINT UNSIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "SMALLINT SIGNED",
                new MyDataType.MySmallInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "SMALLINT SIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "SMALLINT",
                new MyDataType.MySmallInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "SMALLINT" };
            yield return new TheoryDataRow<string, DataType>(
                "MEDIUMINT UNSIGNED",
                new MyDataType.MyMediumInt { Width = null, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "MEDIUMINT UNSIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "MEDIUMINT SIGNED",
                new MyDataType.MyMediumInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "MEDIUMINT SIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "MEDIUMINT",
                new MyDataType.MyMediumInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "MEDIUMINT" };
            yield return new TheoryDataRow<string, DataType>(
                "BIGINT UNSIGNED",
                new MyDataType.MyBigInt { Width = null, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "BIGINT UNSIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "BIGINT SIGNED",
                new MyDataType.MyBigInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "BIGINT SIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "BIGINT",
                new MyDataType.MyBigInt { Width = null, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "BIGINT" };
            yield return new TheoryDataRow<string, DataType>(
                "FLOAT UNSIGNED",
                new MyDataType.MyFloat(null) { NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "FLOAT UNSIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "FLOAT SIGNED",
                new MyDataType.MyFloat(null) { NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "FLOAT SIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "FLOAT",
                new MyDataType.MyFloat(null) { NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "FLOAT" };
            yield return new TheoryDataRow<string, DataType>(
                "DOUBLE UNSIGNED",
                new MyDataType.MyDouble(null) { NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "DOUBLE UNSIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "DOUBLE SIGNED",
                new MyDataType.MyDouble(null) { NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "DOUBLE SIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "DOUBLE",
                new MyDataType.MyDouble(null) { NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "DOUBLE" };
            yield return new TheoryDataRow<string, DataType>(
                "DECIMAL(15, 3)",
                new MyDataType.MyDecimal(new NumericLength.PrecisionScale(15, 3)) { NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "DECIMAL(15,3)" };
            yield return new TheoryDataRow<string, DataType>(
                "DECIMAL UNSIGNED",
                new MyDataType.MyDecimal(new NumericLength.PrecisionScale(10, 0)) { NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "DECIMAL UNSIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "DECIMAL SIGNED",
                new MyDataType.MyDecimal(new NumericLength.PrecisionScale(10, 0)) { NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "DECIMAL SIGNED" };
            yield return new TheoryDataRow<string, DataType>(
                "DECIMAL",
                new MyDataType.MyDecimal(new NumericLength.PrecisionScale(10, 0)) { NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "DECIMAL" };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void CreateIndex_AttachesKeyToTable()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT NOT NULL,
            `name` VARCHAR(100),
            PRIMARY KEY (`id`)
        );
        CREATE INDEX `idx_name` ON `schema1`.`table1` (`name`);
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);
        var def = DefinitionBuilder.ToDefinition();

        var table = def.Tables.Values.First(t => t.Name.Name == "table1");
        Assert.Contains(table.Keys.Values, k => k.Name?.Name == "idx_name");
    }

    [Fact]
    public void CreateUniqueIndex_AttachesUniqueKeyToTable()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT NOT NULL,
            `name` VARCHAR(100),
            PRIMARY KEY (`id`)
        );
        CREATE UNIQUE INDEX `uq_name` ON `schema1`.`table1` (`name`);
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);
        var def = DefinitionBuilder.ToDefinition();

        var table = def.Tables.Values.First(t => t.Name.Name == "table1");
        Assert.Contains(table.UniqueKeys.Values, k => k.Name?.Name == "uq_name");
    }

    [Fact]
    public void CreateIndex_OnUnknownTable_Throws()
    {
        var statementText = """
        CREATE INDEX `idx_x` ON `schema1`.`missing_table` (`name`);
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);
        Assert.Throws<Core.Errors.SqlSyntaxException.CreateIndexUnknownTable>(() => DefinitionBuilder.ToDefinition());
    }

    [Fact]
    public void GetBaseDefinition_CalledTwice_DoesNotReapplyIndexes()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT NOT NULL,
            `name` VARCHAR(100),
            PRIMARY KEY (`id`)
        );
        CREATE INDEX `idx_name` ON `schema1`.`table1` (`name`);
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def1 = DefinitionBuilder.ToDefinition();
        var def2 = DefinitionBuilder.ToDefinition();

        var table1 = def1.Tables.Values.First(t => t.Name.Name == "table1");
        var table2 = def2.Tables.Values.First(t => t.Name.Name == "table1");
        Assert.Single(table1.Keys.Values, k => k.Name?.Name == "idx_name");
        Assert.Single(table2.Keys.Values, k => k.Name?.Name == "idx_name");
    }
}
