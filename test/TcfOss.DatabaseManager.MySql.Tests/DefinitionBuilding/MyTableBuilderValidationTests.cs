using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;
using TcfOss.DatabaseManager.MySql.StatementAnalysis;

namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding;

public class MyTableBuilderValidationTests
{
    [Theory]
    [InlineData([new[] { "col1 INT", "col2 INT", "col1 INT" }])]
    public void Duplicate_ColumnName_Throw(string[] columnsSql)
    {
        var statementColumns = GetColumns(columnsSql);

        var tableBuilder = GetTableBuilder();

        var exception = Assert.Throws<SqlSyntaxException.DuplicateColumn>(() =>
        {
            foreach (var col in statementColumns)
            {
                tableBuilder.AddColumn(col, null);
            }
        });

        var expectedMessage = "Syntax Error: The column name 'col1' is used more than once in table or view '`def`.`schema1`.`myTable`'.";
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(new[] { "col1 INT PRIMARY KEY PRIMARY KEY" }, "PRIMARY KEY")]
    [InlineData(new[] { "col1 INT UNIQUE UNIQUE" }, "UNIQUE")]
    [InlineData(new[] { "col1 INT NULL NOT NULL" }, "nullability")]
    [InlineData(new[] { "col1 INT NULL DEFAULT 5 DEFAULT 10" }, "default")]
    [InlineData(new[] { "col1 INT NULL CHECK (col1 > 5) CHECK (col1 < 4)" }, "CHECK")]
    [InlineData(new[] { "col1 INT NULL GENERATED ALWAYS AS (col1 + 1) AS (col1 + 2)" }, "GENERATED")]
    [InlineData(new[] { "col1 INT NULL COMMENT 'test' UNIQUE COMMENT 'another'" }, "COMMENT")]
    [InlineData(new[] { "col1 INT AUTO_INCREMENT NOT NULL AUTO_INCREMENT" }, "AUTO_INCREMENT")]
    [InlineData(new[] { "col1 INT NULL ON UPDATE (CURRENT_TIMESTAMP) ON UPDATE (NOW)" }, "ON UPDATE")]
    public void Duplicate_AttributeOnColumn_Throw(string[] columnsSql, string duplicateAttribute)
    {
        var statementColumns = GetColumns(columnsSql);

        var tableBuilder = GetTableBuilder();

        var exception = Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() =>
        {
            foreach (var col in statementColumns)
            {
                tableBuilder.AddColumn(col, null);
            }
        });

        var expectedMessage = $"Syntax Error: The attribute '{duplicateAttribute}' is specified multiple times on column `def`.`schema1`.`myTable`.`col1`.";
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData("col1 INT UNIQUE", "UNIQUE", typeof(DefinitionException.UniqueColumn))]
    [InlineData("col1 INT CHECK (col1 > 5)", "CHECK", typeof(DefinitionException.CheckColumn))]
    public void NotAllowedOnColumn_Throws(string columnSql, string attributeName, Type exceptionType)
    {
        var statementColumns = GetColumns([columnSql]);

        var tableBuilder = GetTableBuilder(allowUniqueOnColumn: false, allowCheckOnColumn: false);

        var exception = Assert.ThrowsAny<DefinitionException>(() =>
        {
            foreach (var col in statementColumns)
            {
                tableBuilder.AddColumn(col, null);
            }

        });

        Assert.IsType(exceptionType, exception);

        Assert.Contains("Unsupported in Definitions Error:", exception.Message);
        Assert.Contains(attributeName, exception.Message);
    }

    [Fact]
    public void DuplicateTablePrimaryKey_DirectOnTable_Throws()
    {
        var statementColumns = GetColumns(["col1 INT", "col2 INT"]);

        var tableBuilder = GetTableBuilder();

        tableBuilder.AddColumn(statementColumns[0], null);
        tableBuilder.AddColumn(statementColumns[1], null);

        var pkConstraint = new StatementTableConstraint.PrimaryKey([new KeyPart.Column(new Identifier("col1"))]);

        tableBuilder.AddPrimaryKey(pkConstraint, null);

        var exception = Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() =>
        {
            tableBuilder.AddPrimaryKey(pkConstraint, null);
        });

        var expectedMessage = "Syntax Error: The attribute 'PRIMARY KEY' is specified multiple times on table `def`.`schema1`.`myTable`.";
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void DuplicateTablePrimaryKey_TableAndColumn_Throws()
    {
        var statementColumns = GetColumns(["col1 INT PRIMARY KEY", "col2 INT"]);

        var tableBuilder = GetTableBuilder();

        tableBuilder.AddColumn(statementColumns[0], null);
        tableBuilder.AddColumn(statementColumns[1], null);

        var pkConstraint = new StatementTableConstraint.PrimaryKey([new KeyPart.Column(new Identifier("col1"))]);

        var exception = Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() =>
        {
            tableBuilder.AddPrimaryKey(pkConstraint, null);
        });

        var expectedMessage = "Syntax Error: The attribute 'PRIMARY KEY' is specified multiple times on table `def`.`schema1`.`myTable`.";
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData("col1 INT CONSTRAINT myconstraint NOT NULL", "nullability")]
    [InlineData("col1 INT CONSTRAINT myconstraint DEFAULT 5", "DEFAULT")]
    [InlineData("col1 INT CONSTRAINT myconstraint CHECK (col1 > 5)", "CHECK")]
    [InlineData("col1 INT CONSTRAINT myconstraint UNIQUE", "UNIQUE")]
    public void AttributeNameNotAllowed_Throws(string columnSql, string attributeName)
    {
        var statementColumns = GetColumns([columnSql]);

        var tableBuilder = GetTableBuilder();

        var exception = Assert.Throws<SqlSyntaxException.ConstraintNameNotAllowedException>(() =>
        {
            foreach (var col in statementColumns)
            {
                tableBuilder.AddColumn(col, null);
            }
        });

        Assert.Equal($"Syntax Error: The constraint name 'myconstraint' is not allowed for {attributeName} constraints on column `def`.`schema1`.`myTable`.`col1` in MySQL.", exception.Message);
    }

    [Theory]
    [InlineData("UNIQUE KEY (col1)", "UNIQUE constraint")]
    [InlineData("FOREIGN KEY (col1) REFERENCES othertable(col1)", "FOREIGN KEY constraint")]
    [InlineData("CHECK (col1 > 5)", "CHECK constraint")]
    [InlineData("KEY (col1)", "INDEX")]
    public void ConstraintNameRequired_Throws(string constraintSql, string constraintType)
    {
        var tableConstraints = GetConstraints([constraintSql]);
        var tableBuilder = GetTableBuilder();

        var exception = Assert.Throws<DefinitionException.TableConstraintNameRequired>(() =>
        {
            tableConstraints.ForEach(tableConstraint =>
            {
                tableBuilder.AddIndexOrConstraint(tableConstraint, null);

            });
        });

        var message = $"Unsupported in Definitions Error: {constraintType} on table '`def`.`schema1`.`myTable`' must be named.";
        Assert.Equal(message, exception.Message);
    }

    /// <summary>
    /// Same as the version in TableParser, but without the duplicate check.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="parser"></param>
    /// <returns></returns>
    private static StatementColumn ParseSingleColumn(ParserState state, MyParser parser)
    {
        var name = parser.ComponentParser.ParseIdentifier(state);
        var dataType = parser.DataTypeParser.ParseDataType(state);

        SqlValueList<StatementColumnOption> options = [];

        while (true)
        {
            var columnOption = parser.TableParser.ParseNextColumnOption(state);
            if (columnOption == null)
            {
                break;
            }
            options.Add(columnOption);
        }

        return new StatementColumn(name, dataType, options);
    }

    private static List<StatementTableConstraint> GetConstraints(string[] constraintSql)
    {
        var lexer = new MyLexer();
        var parser = new MyParser();

        var statementConstraints = constraintSql
            .Select(sql =>
            {
                var tokens = lexer.Tokenize(sql);
                var state = new ParserState([.. tokens]);
                return parser.TableParser.ParseOptionalTableConstraint(state)!;
            }).ToList();

        return statementConstraints;
    }

    private static List<StatementColumn> GetColumns(string[] columnSql)
    {
        var lexer = new MyLexer();
        var parser = new MyParser();

        var statementColumns = columnSql
            .Select(sql =>
            {
                var tokens = lexer.Tokenize(sql);
                var state = new ParserState([.. tokens]);
                return ParseSingleColumn(state, parser);
            }).ToList();

        return statementColumns;
    }

    private static MyTableBuilder GetTableBuilder(bool allowUniqueOnColumn = true, bool allowCheckOnColumn = true)
    {
        var validationSettings = new ValidationSettings
        {
            AllowUniqueOnColumn = allowUniqueOnColumn,
            AllowCheckOnColumn = allowCheckOnColumn,
        };
        var config = TestConfig.GetMyTestConfig(validationSettings);
        var componentNormalizer = new MyComponentNormalizer(config.QuoteStyle, new MyFunctionNameProvider());
        var expressionNormalizer = new MyNormalizer(config, componentNormalizer);
        var tableBuilder = new MyTableBuilder(
            new ObjectIdentifier("`myTable`", config.Schemas.Keys.First()),
            config,
            config.Schemas.Values.First().SchemaDefaults,
            componentNormalizer,
            expressionNormalizer,
            null);
        return tableBuilder;
    }
}
