using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Tests.StatementAnalysis;

public class StatementReferencedItemsTests
{
    // ===== Yield-break statements =====

    [Fact]
    public void Commit_NoReferences()
    {
        var stmt = new Commit();
        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })];
        Assert.Empty(items);
    }

    [Fact]
    public void Rollback_NoReferences()
    {
        var stmt = new Rollback();
        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })];
        Assert.Empty(items);
    }

    [Fact]
    public void StartTransaction_NoReferences()
    {
        var stmt = new StartTransaction();
        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })];
        Assert.Empty(items);
    }

    // ===== DML statements =====

    [Fact]
    public void Delete_FromAndWhere_Unknown()
    {
        var stmt = ParseStatement("DELETE FROM t WHERE t.col1 = 1");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(2, items.Count);
        Assert.Equal("t", items[0].N());
        Assert.Equal("t.col1", items[1].N());
    }

    [Fact]
    public void Delete_FromResolvesTable()
    {
        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("mytable")], schema, NameHandling.None)] = ObjectType.Table,
        });
        var pseudoTables = new PseudoTableSet(schema, []);

        var stmt = ParseStatement("DELETE FROM mytable WHERE 1 = 1");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table,
            Definition = provider,
            ActiveSchema = schema,
            PseudoTables = pseudoTables,
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", item.N());
    }

    [Fact]
    public void Insert_TargetAndSource_Unknown()
    {
        var stmt = ParseStatement("INSERT INTO t (a, b) SELECT x, y FROM s");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        // t (InsertTarget), s (FromClause), x, y (SelectItem)
        Assert.Equal(4, items.Count);
        Assert.Equal("t", items[0].N());
        Assert.Equal("s", items[1].N());
        Assert.Equal("x", items[2].N());
        Assert.Equal("y", items[3].N());
    }

    [Fact]
    public void Update_TableAndAssignmentsAndWhere_Unknown()
    {
        var stmt = ParseStatement("UPDATE t SET col1 = col2 + 1 WHERE col3 = 2");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        // t (UpdateClause), col2 (UpdateValue), col3 (WhereClause)
        Assert.Equal(3, items.Count);
        Assert.Equal("t", items[0].N());
        Assert.Equal("col2", items[1].N());
        Assert.Equal("col3", items[2].N());
    }

    [Fact]
    public void Update_ResolvesTableType()
    {
        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("mytable")], schema, NameHandling.None)] = ObjectType.Table,
        });
        var pseudoTables = new PseudoTableSet(schema, []);

        var stmt = ParseStatement("UPDATE mytable SET col1 = 1");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table,
            Definition = provider,
            ActiveSchema = schema,
            PseudoTables = pseudoTables,
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", item.N());
    }

    // ===== DDL statements =====

    [Fact]
    public void DropTable_Unknown()
    {
        var stmt = ParseStatement("DROP TABLE t");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        var item = Assert.Single(items);
        Assert.Equal("t", item.N());
    }

    [Fact]
    public void DropTable_ResolvesTable()
    {
        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("mytable")], schema, NameHandling.None)] = ObjectType.Table,
        });
        var pseudoTables = new PseudoTableSet(schema, []);

        var stmt = ParseStatement("DROP TABLE mytable");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table,
            Definition = provider,
            ActiveSchema = schema,
            PseudoTables = pseudoTables,
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", item.N());
    }

    [Fact]
    public void DropTemporaryTable_ResolvesTable()
    {
        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("mytable")], schema, NameHandling.None)] = ObjectType.Table,
        });
        var pseudoTables = new PseudoTableSet(schema, []);

        var stmt = ParseStatement("DROP TEMPORARY TABLE mytable");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table,
            Definition = provider,
            ActiveSchema = schema,
            PseudoTables = pseudoTables,
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", item.N());
    }

    [Fact]
    public void DropMultipleTables_Unknown()
    {
        var stmt = ParseStatement("DROP TABLE t1, t2");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(2, items.Count);
        Assert.Equal("t1", items[0].N());
        Assert.Equal("t2", items[1].N());
    }

    [Fact]
    public void DropIndex_Unknown()
    {
        var stmt = ParseStatement("DROP INDEX idx ON t");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(2, items.Count);
        Assert.Equal("idx", items[0].N());
        Assert.Equal("t", items[1].N());
    }

    [Fact]
    public void Truncate_Unknown()
    {
        var stmt = ParseStatement("TRUNCATE TABLE t");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        var item = Assert.Single(items);
        Assert.Equal("t", item.N());
    }

    [Fact]
    public void Truncate_ResolvesTable()
    {
        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("mytable")], schema, NameHandling.None)] = ObjectType.Table,
        });
        var pseudoTables = new PseudoTableSet(schema, []);

        var stmt = ParseStatement("TRUNCATE TABLE mytable");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table,
            Definition = provider,
            ActiveSchema = schema,
            PseudoTables = pseudoTables,
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", item.N());
    }

    [Fact]
    public void AlterTable_Unknown()
    {
        var stmt = ParseStatement("ALTER TABLE t ADD COLUMN col1 INT");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        var item = Assert.Single(items);
        Assert.Equal("t", item.N());
    }

    [Fact]
    public void CreateView_ReferencesBodyTables()
    {
        var stmt = ParseStatement("CREATE VIEW v AS SELECT a, b FROM t");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(3, items.Count);
        Assert.Equal("t", items[0].N());
        Assert.Equal("a", items[1].N());
        Assert.Equal("b", items[2].N());
    }

    [Fact]
    public void CreateTableAsSelect_ReferencesSourceTables()
    {
        var stmt = ParseStatement("CREATE TABLE t AS SELECT a, b FROM s");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(3, items.Count);
        Assert.Equal("s", items[0].N());
        Assert.Equal("a", items[1].N());
        Assert.Equal("b", items[2].N());
    }

    [Fact]
    public void CreateTableNoSelect_NoReferences()
    {
        var stmt = ParseStatement("CREATE TABLE t (col1 INT, col2 VARCHAR(50))");

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })];

        Assert.Empty(items);
    }

    // ===== Compound statements =====

    [Fact]
    public void If_ReferencesConditionAndBody()
    {
        var ifStmt = new If.My(
            ReferencedItemsTests.ParseExpression("a > 1"),
            [new SetVariable([new Assignment(
                new AssignmentTarget.ObjectName(new ObjectName([new Identifier("x")])),
                ReferencedItemsTests.ParseExpression("b + c"))])]);

        List<ItemRef> items = [.. ifStmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(3, items.Count);
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
        Assert.Equal("c", items[2].N());
    }

    [Fact]
    public void If_ReferencesElseIfConditionAndBody()
    {
        var ifStmt = new If.My(
            ReferencedItemsTests.ParseExpression("a > 1"),
            [new SetVariable([new Assignment(
                new AssignmentTarget.ObjectName(new ObjectName([new Identifier("x")])),
                ReferencedItemsTests.ParseExpression("b"))])])
        {
            ElseIfs =
            [
                new If.ElseIf(
                    ReferencedItemsTests.ParseExpression("c > 2"),
                    [new SetVariable([new Assignment(
                        new AssignmentTarget.ObjectName(new ObjectName([new Identifier("y")])),
                        ReferencedItemsTests.ParseExpression("d + e"))])])
            ]
        };

        List<ItemRef> items = [.. ifStmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(5, items.Count);
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
        Assert.Equal("c", items[2].N());
        Assert.Equal("d", items[3].N());
        Assert.Equal("e", items[4].N());
    }

    [Fact]
    public void If_ReferencesElseStatements()
    {
        var ifStmt = new If.My(
            ReferencedItemsTests.ParseExpression("a > 1"),
            [new SetVariable([new Assignment(
                new AssignmentTarget.ObjectName(new ObjectName([new Identifier("x")])),
                ReferencedItemsTests.ParseExpression("b"))])])
        {
            ElseStatements =
            [
                new SetVariable([new Assignment(
                    new AssignmentTarget.ObjectName(new ObjectName([new Identifier("z")])),
                    ReferencedItemsTests.ParseExpression("c + d"))])
            ]
        };

        List<ItemRef> items = [.. ifStmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(4, items.Count);
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
        Assert.Equal("c", items[2].N());
        Assert.Equal("d", items[3].N());
    }

    [Fact]
    public void BeginEnd_DelegatesToChildren()
    {
        var stmt = new BeginEnd(
        [
            new SetVariable([new Assignment(
                new AssignmentTarget.ObjectName(new ObjectName([new Identifier("x")])),
                ReferencedItemsTests.ParseExpression("a + b"))]),
            new SetVariable([new Assignment(
                new AssignmentTarget.ObjectName(new ObjectName([new Identifier("y")])),
                ReferencedItemsTests.ParseExpression("c + d"))]),
        ]);

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(4, items.Count);
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
        Assert.Equal("c", items[2].N());
        Assert.Equal("d", items[3].N());
    }

    [Fact]
    public void Return_ReferencesExpression()
    {
        var stmt = new Return(ReferencedItemsTests.ParseExpression("a + b"));

        List<ItemRef> items = [.. stmt.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
    }

    // ===== Parse helpers =====

    private static Statement ParseStatement(string sql)
    {
        var tokens = new GenericLexer().Tokenize(sql);
        var state = new ParserState([.. tokens]);
        return new Parser().ParseStatement(state);
    }
}
