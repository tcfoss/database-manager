using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding;

public class PseudoTableTests
{
    private static MyDefinition CreateDefinition()
    {
        var tp = new TextParser(new MyLexer(), new MyParser());
        var config = TestConfig.MyTestConfig;
        var loggerFactory = new LoggerFactory();

        var builder = new MyDefinitionBuilder(
            config,
            new SourceManager(),
            new MyFunctionNameProvider(),
            loggerFactory.CreateLogger<MyDefinitionBuilder>()
        );

        var text = """
        CREATE TABLE schema1.table1 (
            id INT NOT NULL,
            name VARCHAR(100),
            PRIMARY KEY (id)
        );

        CREATE TABLE schema1.table2 (
            id INT NOT NULL,
            parent_id INT NOT NULL,
            description TEXT,
            PRIMARY KEY (id)
        );

        CREATE TABLE schema1.table5 (
            id INT NOT NULL,
            info VARCHAR(255),
            PRIMARY KEY (id)
        );

        CREATE TABLE schema2.table3 (
            id INT NOT NULL,
            value DECIMAL(10,2),
            PRIMARY KEY (id)
        );

        CREATE TABLE schema2.table4 (
            id INT NOT NULL,
            parent_id INT NOT NULL,
            details TEXT,
            PRIMARY KEY (id)
        );

        CREATE
            DEFINER = 'root'@'localhost'
            SQL SECURITY DEFINER
        VIEW schema1.view1 AS
        WITH cte1 AS (
            SELECT id, value
            FROM schema2.table3
            WHERE id > 10
        )
        SELECT t1.id,
               t1.name,
               t2.description,
               cte1.value,
               derived_table.info
        FROM schema1.table1 t1
        JOIN schema1.table2 t2
            ON t1.id = t2.parent_id
        LEFT JOIN schema2.table4 t4
            ON t2.id = t4.parent_id
        JOIN cte1
            ON t1.id = cte1.id
        JOIN (
            SELECT id, info
            FROM schema1.table5
            WHERE id < 100
        ) AS derived_table
            ON t1.id = derived_table.id;
        """;

        builder.ProcessStatements(tp.ParseText(text), config.Schemas.Keys.First(), 0);
        return builder.ToDefinition();
    }


    [Fact]
    public void PseudoTables()
    {
        var definition = CreateDefinition();
        var pseudoTableSet = definition.ToPseudoTableSet(new SchemaIdentifier("schema1", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks));

        var viewTable = pseudoTableSet.GetRequiredPseudoTable(new ObjectName([new Identifier("schema1"), new Identifier("view1")]));

        Assert.Equal(5, viewTable.SelectableItems.Count);
        Assert.Contains("id", viewTable.SelectableItems);
        Assert.Contains("name", viewTable.SelectableItems);
        Assert.Contains("description", viewTable.SelectableItems);
        Assert.Contains("value", viewTable.SelectableItems);
        Assert.Contains("info", viewTable.SelectableItems);
    }
}
