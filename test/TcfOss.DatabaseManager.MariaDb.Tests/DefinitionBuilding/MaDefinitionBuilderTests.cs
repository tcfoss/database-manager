using System.Collections;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MariaDb.Configuration;
using TcfOss.DatabaseManager.MariaDb.Tests.Configuration;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Tests.Configuration;
using TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding;
using Xunit.Sdk;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

[assembly: RegisterXunitSerializer(typeof(MyDefinitionBuilderTestsBase.DataTypeSerializer), typeof(DataType))]

namespace TcfOss.DatabaseManager.MariaDb.Tests.DefinitionBuilding;

public class MaDefinitionBuilderTests : MyDefinitionBuilderTestsBase
{
    protected override bool Relaxed => false;
    protected override MyConfig MyConfig { get; }
    protected override MyDefinitionBuilder DefinitionBuilder { get; }
    protected override string DefaultCollation => "utf8mb4_uca1400_ai_ci";
    protected override string DefaultIntegerWidth => "(11)";
    protected override string DefaultUnsignedIntegerWidth => "(10)";
    protected override RoutineParameterDirection FunctionParameterDirection => RoutineParameterDirection.In;
    protected virtual Func<MyConfig, ILogger<MyDefinitionBuilder>, SourceManager, MyDefinitionBuilder> DefinitionBuilderFactory => (config, logger, sourceManager) => new MyDefinitionBuilder(config, sourceManager, new MyFunctionNameProvider(), logger);

    public MaDefinitionBuilderTests()
    {
        // ReSharper disable VirtualMemberCallInConstructor
        var differFormatSettings = new ConfigParsing.DifferFormattingSettings
        {
            OmitModifiersIfDefault = false,
            ViewPreferNormalizedBody = true,
        };
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig(
            dialect: SqlDialect.MariaDb,
            projectDirectory: "/fake/project/root",
            differFormattingSettings: differFormatSettings);
        var logger = new LoggerFactory().CreateLogger<MyDefinitionBuilder>();
        var otherInfo = MaGetOtherData.GetData(rawConfig);
        MyConfig = new MaConfigLoader(logger).LoadConfig("/home/username/database", rawConfig, otherInfo, Relaxed);
        DefinitionBuilder = GetDefinitionBuilder();
        // ReSharper restore VirtualMemberCallInConstructor
    }

    protected override MyDefinitionBuilder GetDefinitionBuilder()
    {
        return DefinitionBuilderFactory(MyConfig, new LoggerFactory().CreateLogger<MyDefinitionBuilder>(), new SourceManager());
    }


    [Theory]
    [ClassData(typeof(MaDefaultsAppliedTestData))]
    public void Ma_Test_Defaults_Applied_To_Column(string columnDefinition, DataType expectedDataType)
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
        var table = Assert.Single(def.Tables.Values);
        var column = Assert.Single(table.Columns);
        Assert.Equal(expectedDataType, column.DataType);
    }

    private class MaDefaultsAppliedTestData : IEnumerable<TheoryDataRow<string, DataType>>
    {
        public IEnumerator<TheoryDataRow<string, DataType>> GetEnumerator()
        {
            yield return new TheoryDataRow<string, DataType>(
                "INT UNSIGNED",
                new MyDataType.MyInt { Width = 10, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "INT UNSIGNED (10)" };
            yield return new TheoryDataRow<string, DataType>(
                "INT SIGNED",
                new MyDataType.MyInt { Width = 11, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "INT SIGNED (11)" };
            yield return new TheoryDataRow<string, DataType>(
                "INT",
                new MyDataType.MyInt { Width = 11, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "INT (11)" };
            yield return new TheoryDataRow<string, DataType>(
                "TINYINT UNSIGNED",
                new MyDataType.MyTinyInt { Width = 3, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "TINYINT UNSIGNED (3)" };
            yield return new TheoryDataRow<string, DataType>(
                "TINYINT SIGNED",
                new MyDataType.MyTinyInt { Width = 4, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "TINYINT SIGNED (4)" };
            yield return new TheoryDataRow<string, DataType>(
                "TINYINT",
                new MyDataType.MyTinyInt { Width = 4, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "TINYINT (4)" };
            yield return new TheoryDataRow<string, DataType>(
                "SMALLINT UNSIGNED",
                new MyDataType.MySmallInt { Width = 5, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "SMALLINT UNSIGNED (5)" };
            yield return new TheoryDataRow<string, DataType>(
                "SMALLINT SIGNED",
                new MyDataType.MySmallInt { Width = 6, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "SMALLINT SIGNED (6)" };
            yield return new TheoryDataRow<string, DataType>(
                "SMALLINT",
                new MyDataType.MySmallInt { Width = 6, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "SMALLINT (6)" };
            yield return new TheoryDataRow<string, DataType>(
                "MEDIUMINT UNSIGNED",
                new MyDataType.MyMediumInt { Width = 8, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "MEDIUMINT UNSIGNED (8)" };
            yield return new TheoryDataRow<string, DataType>(
                "MEDIUMINT SIGNED",
                new MyDataType.MyMediumInt { Width = 9, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "MEDIUMINT SIGNED (9)" };
            yield return new TheoryDataRow<string, DataType>(
                "MEDIUMINT",
                new MyDataType.MyMediumInt { Width = 9, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "MEDIUMINT (9)" };
            yield return new TheoryDataRow<string, DataType>(
                "BIGINT UNSIGNED",
                new MyDataType.MyBigInt { Width = 20, NumericAttribute = MySqlNumericAttribute.Unsigned })
            { Label = "BIGINT UNSIGNED (20)" };
            yield return new TheoryDataRow<string, DataType>(
                "BIGINT SIGNED",
                new MyDataType.MyBigInt { Width = 20, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "BIGINT SIGNED (20)" };
            yield return new TheoryDataRow<string, DataType>(
                "BIGINT",
                new MyDataType.MyBigInt { Width = 20, NumericAttribute = MySqlNumericAttribute.Signed })
            { Label = "BIGINT (20)" };
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
    public void Function_Supports_Parameter_Direction()
    {
        var statementText = """
        CREATE
            DEFINER = `root`@`localhost`
        FUNCTION `schema1`.`my_function` (
            IN `param1` INT,
            IN `param2` VARCHAR(100)
        )
        RETURNS INT
            DETERMINISTIC
            SQL SECURITY DEFINER
            COMMENT 'This is a test function'
        BEGIN
            RETURN 1;
        END;
        """;
        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Functions);

        var function = def.Functions.Values.First();
        var parameters = function.Parameters.ToList();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("param1", parameters[0].Name.Name);
        Assert.Equal($"INT{DefaultIntegerWidth} SIGNED", parameters[0].DataType.ToSql());
        Assert.Equal(RoutineParameterDirection.In, Assert.IsType<RoutineParameter.Directed>(parameters[0]).Direction);
        Assert.Equal("param2", parameters[1].Name.Name);
        Assert.Equal($"VARCHAR(100) CHARACTER SET utf8mb4{DefaultCollationSetter}", parameters[1].DataType.ToSql());
        Assert.Equal(RoutineParameterDirection.In, Assert.IsType<RoutineParameter.Directed>(parameters[1]).Direction);
    }

    [Fact]
    public void Function_Supports_Parameters_Out()
    {
        var statementText = """
        CREATE
            DEFINER = `root`@`localhost`
        FUNCTION `schema1`.`my_function` (
            IN `param1` INT,
            OUT `param2` VARCHAR(100)
        )
        RETURNS INT
            DETERMINISTIC
            SQL SECURITY DEFINER
            COMMENT 'This is a test function'
        BEGIN
            RETURN 1;
        END;
        """;
        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Functions);

        var function = def.Functions.Values.First();
        var parameters = function.Parameters.ToList();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("param1", parameters[0].Name.Name);
        Assert.Equal($"INT{DefaultIntegerWidth} SIGNED", parameters[0].DataType.ToSql());
        Assert.Equal(RoutineParameterDirection.In, Assert.IsType<RoutineParameter.Directed>(parameters[0]).Direction);
        Assert.Equal("param2", parameters[1].Name.Name);
        Assert.Equal($"VARCHAR(100) CHARACTER SET utf8mb4{DefaultCollationSetter}", parameters[1].DataType.ToSql());
        Assert.Equal(RoutineParameterDirection.Out, Assert.IsType<RoutineParameter.Directed>(parameters[1]).Direction);
    }

    [Fact]
    public void ColumnCheck_Nesting_KeptInExpression_RemovedInNormalized_EqualityBasedOnNormalized()
    {
        var statementText1 = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            threshold INT NOT NULL,
            checked INT CHECK (IF(checked < threshold, checked * (threshold - checked), 0))
        );
        """;

        var db1 = GetDefinitionBuilder();
        var statements1 = TextParser.ParseText(statementText1);
        db1.ProcessStatements(statements1, Schema, 0);

        var def1 = db1.ToDefinition();
        var table1 = def1.Tables.Values.First(x => x.Name.Name == "mytable");
        var checkedColumn1 = table1.Columns.First(c => c.Name.Name == "checked");
        var check1 = checkedColumn1.Check;

        Assert.NotNull(check1);

        var statementText2 = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            threshold INT NOT NULL,
            checked INT CHECK (IF(checked < threshold, checked * ((threshold - checked)), 0))
        );
        """;

        var db2 = GetDefinitionBuilder();
        var statements2 = TextParser.ParseText(statementText2);
        db2.ProcessStatements(statements2, Schema, 0);

        var def2 = db2.ToDefinition();
        var table2 = def2.Tables.Values.First(x => x.Name.Name == "mytable");
        var checkedColumn2 = table2.Columns.First(c => c.Name.Name == "checked");
        var check2 = checkedColumn2.Check;

        Assert.NotNull(check2);

        Assert.Equal(check1, check2);

        Assert.Equal(check1.NormalizedExpression, check2.NormalizedExpression);
        Assert.NotEqual(check1.Expression, check2.Expression);

        Assert.Contains("((`threshold` - `checked`))", check2.Expression.ToSql());
        Assert.DoesNotContain("((`threshold` - `checked`))", check1.Expression.ToSql());

        var create1 = table1.ToCreateStatement(true, GetDifferFormatManager()).ToSql();
        var create2 = table2.ToCreateStatement(true, GetDifferFormatManager()).ToSql();
        Assert.NotEqual(create1, create2);
        Assert.Contains("((`threshold` - `checked`))", create2);

        var thenRight1 = ExtractThenRightOperand(check1.Expression);
        var thenRight2 = ExtractThenRightOperand(check2.Expression);
        var thenRight1Normalized = ExtractThenRightOperand(check1.NormalizedExpression);
        var thenRight2Normalized = ExtractThenRightOperand(check2.NormalizedExpression);

        Assert.Equal(1, CountNestedDepth(thenRight1));
        Assert.Equal(2, CountNestedDepth(thenRight2));
        Assert.IsType<TcfOss.DatabaseManager.Core.Expressions.BinaryOperator>(thenRight1Normalized);
        Assert.IsType<TcfOss.DatabaseManager.Core.Expressions.BinaryOperator>(thenRight2Normalized);
    }
}
