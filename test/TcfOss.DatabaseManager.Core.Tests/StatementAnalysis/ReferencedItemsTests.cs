using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Tests.StatementAnalysis;

public class ReferencedItemsTests
{
    // ===== Expression-level tests =====

    [Fact]
    public void SimpleExpression_Unknown()
    {
        var text = "a + b * c - d / e";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(5, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
        Assert.Equal("c", items[2].N());
        Assert.Equal("d", items[3].N());
        Assert.Equal("e", items[4].N());
    }

    [Fact]
    public void SimpleExpressionQualified_Unknown()
    {
        var text = "t.a + t.b + t.c - t.d / t.e";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(5, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("t.a", items[0].N());
        Assert.Equal("t.b", items[1].N());
        Assert.Equal("t.c", items[2].N());
        Assert.Equal("t.d", items[3].N());
        Assert.Equal("t.e", items[4].N());
    }

    [Fact]
    public void ComplexExpression_Unknown_1()
    {
        var text = "(a + b) * (c - d) / e + FUNC(f, g, h) + IF(i BETWEEN j AND k, l, m)";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        // FUNC and IF are not in Definition/FunctionNameProvider → Unknown, so they are included.
        Assert.Equal(15, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
        Assert.Equal("c", items[2].N());
        Assert.Equal("d", items[3].N());
        Assert.Equal("e", items[4].N());
        Assert.Equal("FUNC", items[5].N());
        Assert.Equal("f", items[6].N());
        Assert.Equal("g", items[7].N());
        Assert.Equal("h", items[8].N());
        Assert.Equal("IF", items[9].N());
        Assert.Equal("i", items[10].N());
        Assert.Equal("j", items[11].N());
        Assert.Equal("k", items[12].N());
        Assert.Equal("l", items[13].N());
        Assert.Equal("m", items[14].N());
    }

    [Fact]
    public void ComplexExpression_UnknownAndFunction_1()
    {
        var text = "(a + b) * (c - d) / e + FUNC(f, g, h) + IF(i BETWEEN j AND k, l, m)";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown | ObjectNameFilters.Function })];

        // No Definition/FunctionNameProvider: FUNC and IF resolve to Unknown, not Function.
        Assert.Equal(15, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
        Assert.Equal("c", items[2].N());
        Assert.Equal("d", items[3].N());
        Assert.Equal("e", items[4].N());
        Assert.Equal("FUNC", items[5].N());
        Assert.Equal("f", items[6].N());
        Assert.Equal("g", items[7].N());
        Assert.Equal("h", items[8].N());
        Assert.Equal("IF", items[9].N());
        Assert.Equal("i", items[10].N());
        Assert.Equal("j", items[11].N());
        Assert.Equal("k", items[12].N());
        Assert.Equal("l", items[13].N());
        Assert.Equal("m", items[14].N());
    }

    [Fact]
    public void ComplexExpression_Unknown_2()
    {
        var text = "IF(a IS NOT NULL, b, c) + -IF(d IN (e,f,g), h, i)";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        // Two IF function names + their arguments, all Unknown.
        Assert.Equal(11, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("IF", items[0].N());
        Assert.Equal("a", items[1].N());
        Assert.Equal("b", items[2].N());
        Assert.Equal("c", items[3].N());
        Assert.Equal("IF", items[4].N());
        Assert.Equal("d", items[5].N());
        Assert.Equal("e", items[6].N());
        Assert.Equal("f", items[7].N());
        Assert.Equal("g", items[8].N());
        Assert.Equal("h", items[9].N());
        Assert.Equal("i", items[10].N());
    }

    [Fact]
    public void CaseExpression_Unknown_1()
    {
        var text = "CASE WHEN a > 5 THEN c WHEN b < 10 THEN -d ELSE e END";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        // Case iterates: all conditions (a, b), then all results (c, d), then else (e).
        Assert.Equal(5, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
        Assert.Equal("c", items[2].N());
        Assert.Equal("d", items[3].N());
        Assert.Equal("e", items[4].N());
    }

    [Fact]
    public void CaseExpression_Unknown_2()
    {
        var test = "CASE a WHEN b THEN d WHEN c THEN e ELSE f END";
        var expr = ParseExpression(test);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(6, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
        Assert.Equal("c", items[2].N());
        Assert.Equal("d", items[3].N());
        Assert.Equal("e", items[4].N());
        Assert.Equal("f", items[5].N());
    }

    [Theory]
    [InlineData("CHAR(3) 'Hello'")]
    public void VariousZeroArgumentExpressions(string sql)
    {
        var expr = ParseExpression(sql);

        // These are not FunctionCall nodes in the generic parser; no identifiers are yielded.
        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })];

        Assert.Empty(items);
    }

    [Fact]
    public void CountStar_ReturnsOnlyFunctionName()
    {
        // COUNT(*) is a FunctionCall; the * wildcard has no identifier, but COUNT itself is.
        var expr = ParseExpression("COUNT(*)");

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.All,
            FunctionNameProvider = new FunctionNameProvider(),
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.BuiltInFunction, item.Type);
        Assert.Equal("COUNT", item.N());
    }

    [Theory]
    [InlineData("a IS NULL")]
    [InlineData("a IS NOT NULL")]
    [InlineData("a IS TRUE")]
    [InlineData("a IS NOT TRUE")]
    [InlineData("a IS FALSE")]
    [InlineData("a IS NOT FALSE")]
    [InlineData("a IS UNKNOWN")]
    [InlineData("a IS NOT UNKNOWN")]
    [InlineData("INTERVAL a DAY")]
    [InlineData("a.*")]
    [InlineData("CONVERT(a USING utf8mb4)")]
    [InlineData("CONVERT(a, INT)")]
    [InlineData("CAST(a AS CHAR(1000))")]
    [InlineData("a LIKE 'pattern'")]
    [InlineData("a REGEXP 'pattern'")]
    [InlineData("a RLIKE 'pattern'")]
    [InlineData("a IN (1, 2, 3)")]
    public void VariousSimpleOneComponentExpressions(string sql)
    {
        var expr = ParseExpression(sql);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Unknown, item.Type);
        Assert.Equal("a", item.N());
    }

    [Theory]
    [InlineData("a LIKE b ESCAPE c")]
    [InlineData("a IS DISTINCT FROM b")]
    [InlineData("a IS NOT DISTINCT FROM b")]
    [InlineData("a AT TIME ZONE b")]
    [InlineData("a >= ALL((b, 1, 2))")]
    [InlineData("a = POSITION('mysubstr' IN b)")]
    [InlineData("a = POSITION(b IN 'myotherstring')")]
    [InlineData("a LIKE b")]
    [InlineData("a REGEXP b")]
    [InlineData("a RLIKE b")]
    [InlineData("a IN (b, 'c', 'd')")]
    public void VariousSimpleTwoComponentExpressions(string sql)
    {
        var expr = ParseExpression(sql);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(2, items.Count);
        Assert.Equal(ItemType.Unknown, items[0].Type);
        Assert.Equal(ItemType.Unknown, items[1].Type);
        Assert.Equal("a", items[0].N());
        Assert.Equal("b", items[1].N());
    }

    [Theory]
    [InlineData("a = SOMEFUNC(b)")]
    [InlineData("a = SOMEFUNC(arg = b)")]
    [InlineData("a = SOMEFUNC(arg := b)")]
    public void FunctionCallInExpression_Unknown_IncludesFunctionNameAsUnknown(string sql)
    {
        var expr = ParseExpression(sql);

        // Unrecognized function → Unknown; so a, SOMEFUNC, and b are all returned.
        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("a", items[0].N());
        Assert.Equal("SOMEFUNC", items[1].N());
        Assert.Equal("b", items[2].N());
    }

    [Fact]
    public void FunctionCall_WithDefinition_ClassifiedAsFunction()
    {
        var expr = ParseExpression("myfunc(a)");

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("myfunc")], schema, NameHandling.None)] = ObjectType.Function,
        });

        // Only Function filter: myfunc is returned as Function; argument 'a' (Unknown) is excluded.
        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Function,
            Definition = provider,
            ActiveSchema = schema
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Function, item.Type);
        Assert.Equal("myfunc", item.N());
        Assert.NotNull(item.ObjectHandle);
        Assert.Equal("myfunc", item.ObjectHandle!.Value.Name);
    }

    [Fact]
    public void FunctionCall_WithFunctionNameProvider_BuiltInClassifiedAsBuiltIn()
    {
        var expr = ParseExpression("COALESCE(a, b)");

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.BuiltInFunction,
            FunctionNameProvider = new FunctionNameProvider(),
        })];

        // COALESCE is in FunctionNameProvider → BuiltInFunction. Arguments 'a' and 'b' (Unknown) excluded.
        var item = Assert.Single(items);
        Assert.Equal(ItemType.BuiltInFunction, item.Type);
        Assert.Equal("COALESCE", item.N());
        Assert.Null(item.ObjectHandle);
    }

    [Fact]
    public void FunctionExpression_QualifiedWildcard()
    {
        var text = "MY_FUNC(t.*)";
        var expr = ParseExpression(text);

        // MY_FUNC (unrecognized → Unknown) and t (from qualified wildcard t.*) both Unknown.
        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("MY_FUNC", items[0].N());
        Assert.Equal("t", items[1].N());
    }

    [Fact]
    public void InSubquery_Unknown_ReturnsOuterAndSubqueryItems()
    {
        // With Unknown filter and no definition/pseudotables, all identifiers from all clauses
        // are returned (outer 'a', FROM 'table1', SELECT 'b', WHERE 'c' and 'd').
        var text = "a IN (SELECT b FROM table1 WHERE c = d)";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(5, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("a", items[0].N());
        Assert.Equal("table1", items[1].N());
        Assert.Equal("b", items[2].N());
        Assert.Equal("c", items[3].N());
        Assert.Equal("d", items[4].N());
    }

    [Fact]
    public void InSubquery_WithDefinition_FromClauseItemTyped()
    {
        // With a definition, 'table1' in FROM resolves to Table; only Table items pass.
        var text = "a IN (SELECT b FROM table1 WHERE c = d)";
        var expr = ParseExpression(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
        });

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("table1", item.N());
    }

    // ===== SELECT statement tests =====

    [Fact]
    public void Select_Basic_All_ReturnsEverything()
    {
        var text = """
        SELECT
            t.a,
            t.b,
            t.c
        FROM table1 AS t
        WHERE t.d = 5
        AND   t.e < 10
        """;
        var select = ParseSelect(text);

        // All items from all clauses are returned; without definition, all are Unknown.
        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })];

        Assert.Equal(6, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("table1", items[0].N());
        Assert.Equal("t.a", items[1].N());
        Assert.Equal("t.b", items[2].N());
        Assert.Equal("t.c", items[3].N());
        Assert.Equal("t.d", items[4].N());
        Assert.Equal("t.e", items[5].N());
    }

    [Fact]
    public void Select_Wildcard_ReturnsNothing()
    {
        var text = "SELECT * FROM mytable";
        var select = ParseSelect(text);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })];

        // '*' wildcard yields no items; 'mytable' is Unknown (no definition)
        var item = Assert.Single(items);
        Assert.Equal(ItemType.Unknown, item.Type);
        Assert.Equal("mytable", item.N());
    }

    [Fact]
    public void Select_QualifiedWildcard()
    {
        var text = "SELECT t.* FROM mytable AS t";
        var select = ParseSelect(text);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("mytable", items[0].N());
        Assert.Equal("t", items[1].N());
    }

    [Fact]
    public void Select_Basic_WithDefinition_FromClauseTable()
    {
        var text = """
        SELECT
            t.a,
            t.b,
            t.c
        FROM table1 AS t
        WHERE t.d = 5
        AND   t.e < 10
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
        });

        // Only Table and View items pass the filter; FROM clause items resolve to Table with definition.
        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("table1", item.N());
    }

    [Fact]
    public void Select_Basic_WithPseudoTables_SelectItems()
    {
        var text = """
        SELECT
            t.a,
            t.b,
            t.c
        FROM table1 AS t
        WHERE t.d = 5
        AND   t.e < 10
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var tableIdentifier = new ObjectIdentifier("table1", schema);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
        });
        var pseudoTables = new PseudoTableSet(
            schema,
            [new PseudoTable("table1", tableIdentifier, ["a", "b", "c", "d", "e"], PseudoTableType.Table)]);

        // TableColumn items only — resolves SELECT and WHERE items to TableColumn, FROM items to Table (excluded).
        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.TableColumn,
            Definition = provider,
            PseudoTables = pseudoTables
        })];

        Assert.Equal(5, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.TableColumn, i.Type));
        Assert.Equal("t.a", items[0].N());
        Assert.Equal("t.b", items[1].N());
        Assert.Equal("t.c", items[2].N());
        Assert.Equal("t.d", items[3].N());
        Assert.Equal("t.e", items[4].N());
    }

    [Fact]
    public void Select_WithJoin_WithDefinition_FromClauseItems()
    {
        var text = """
        SELECT
            t.a,
            t.b,
            t.c,
            j.x,
            j.y
        FROM table1 AS t
        JOIN table2 AS j
            ON t.id = j.t_id
        WHERE t.d = 5
        AND   j.z < 10
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create([new Identifier("table2")], schema, NameHandling.None)] = ObjectType.Table,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Table, i.Type));
        Assert.Equal("table1", items[0].N());
        Assert.Equal("table2", items[1].N());
    }

    [Fact]
    public void Select_WithSubquery_WithDefinition_FromClauseItems()
    {
        var text = """
        SELECT
            t.a,
            t.b,
            (SELECT x FROM table2 AS j WHERE j.t_id = t.id) AS subquery_result
        FROM table1 AS t
        WHERE t.d = 5
        AND   t.e < 10
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create([new Identifier("table2")], schema, NameHandling.None)] = ObjectType.Table,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        // Both the outer table1 and the subquery's table2 resolve as Table.
        // FROM is iterated before SELECT, so the outer table1 comes first.
        Assert.Equal(2, items.Count);
        Assert.Equal(ItemType.Table, items[0].Type);
        Assert.Equal("table1", items[0].N());
        Assert.Equal(ItemType.Table, items[1].Type);
        Assert.Equal("table2", items[1].N());
    }

    /// <summary>
    /// FROM clause returns BOTH the main FROM table AND all JOIN tables, since join table
    /// factors live inside the same FromClause context push.
    /// </summary>
    [Fact]
    public void Select_WithJoin_WithDefinition_IncludesBothMainAndJoinTables()
    {
        var text = """
        SELECT
            t.a,
            t.b,
            t.c,
            j.x,
            j.y
        FROM table1 AS t
        JOIN table2 AS j
            ON t.id = j.t_id
        WHERE t.d = 5
        AND   j.z < 10
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create([new Identifier("table2")], schema, NameHandling.None)] = ObjectType.Table,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Table, i.Type));
        Assert.Equal("table1", items[0].N());
        Assert.Equal("table2", items[1].N());
    }

    [Fact]
    public void Select_WithJoin_WithPseudoTables_WhereItems()
    {
        var text = """
        SELECT
            t.a,
            t.b,
            t.c,
            j.x,
            j.y
        FROM table1 AS t
        JOIN table2 AS j
            ON t.id = j.t_id
        WHERE t.d = 5
        AND   j.z < 10
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var table1Identifier = new ObjectIdentifier("table1", schema);
        var table2Identifier = new ObjectIdentifier("table2", schema);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create([new Identifier("table2")], schema, NameHandling.None)] = ObjectType.Table,
        });
        var pseudoTables = new PseudoTableSet(
            schema,
            [
                new PseudoTable("table1", table1Identifier, ["a", "b", "c", "d", "id"], PseudoTableType.Table),
                new PseudoTable("table2", table2Identifier, ["x", "y", "z", "t_id"], PseudoTableType.Table),
            ]);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.TableColumn,
            Definition = provider,
            PseudoTables = pseudoTables
        })];

        // All column refs from JOIN condition, SELECT, and WHERE resolve to TableColumn.
        Assert.Equal(9, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.TableColumn, i.Type));
        Assert.Equal("t.id", items[0].N());
        Assert.Equal("j.t_id", items[1].N());
        Assert.Equal("t.a", items[2].N());
        Assert.Equal("t.b", items[3].N());
        Assert.Equal("t.c", items[4].N());
        Assert.Equal("j.x", items[5].N());
        Assert.Equal("j.y", items[6].N());
        Assert.Equal("t.d", items[7].N());
        Assert.Equal("j.z", items[8].N());
    }

    [Fact]
    public void Select_WithJoin_JoinConditions_Unknown()
    {
        var text = """
        SELECT
            t.a,
            t.b,
            t.c,
            j.x,
            j.y
        FROM table1 AS t
        JOIN table2 AS j
            ON  t.id = j.t_id
            AND j.status = t.status
        WHERE t.d = 5
        AND   j.z < 10
        """;
        var select = ParseSelect(text);

        // Without PseudoTables, all items are Unknown; filter to Unknown gives everything.
        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        // SELECT: t.a, t.b, t.c, j.x, j.y; FROM: table1, table2; JoinCond: t.id, j.t_id, j.status, t.status; WHERE: t.d, j.z
        Assert.Equal(13, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
    }

    [Fact]
    public void Select_NestedJoin_WithDefinition_IncludesAllJoinedTables()
    {
        var text = """
        SELECT
            t.a,
            t.b,
            t.c,
            j.x,
            j.y
        FROM (table1 AS t
        JOIN table2 AS j
            ON (t.id = j.t_id)
        JOIN table3 AS k
            ON (j.id = k.j_id))
        WHERE t.d = 5
        AND   j.z < 10
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create([new Identifier("table2")], schema, NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create([new Identifier("table3")], schema, NameHandling.None)] = ObjectType.Table,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Table, i.Type));
        Assert.Equal("table1", items[0].N());
        Assert.Equal("table2", items[1].N());
        Assert.Equal("table3", items[2].N());
    }

    [Fact]
    public void Select_DerivedTable_WithDefinition_InnerTableAppearsAsTable()
    {
        var text = """
        SELECT
            dt.a,
            dt.b,
            dt.c
        FROM (SELECT id, a, b, c FROM table1) AS dt
        JOIN table2 AS t
            ON dt.id = t.a_id
        WHERE dt.a > 10
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create([new Identifier("table2")], schema, NameHandling.None)] = ObjectType.Table,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        // The derived table itself is anonymous; only the inner table1 and joined table2 are real objects.
        Assert.Equal(2, items.Count);
        Assert.Equal("table1", items[0].N());
        Assert.Equal("table2", items[1].N());
    }

    [Fact]
    public void Select_GroupConcat_Unknown()
    {
        var text = """
        SELECT
            GROUP_CONCAT(t.a ORDER BY t.b LIMIT t.c SEPARATOR ', ') AS concatenated_values
        FROM table1 AS t
        GROUP BY t.c
        """;
        var select = ParseSelect(text);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        // FROM: table1; SELECT: GROUP_CONCAT (function name), t.a, t.b, t.c
        Assert.Equal(5, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("table1", items[0].N());
        Assert.Equal("GROUP_CONCAT", items[1].N());
        Assert.Equal("t.a", items[2].N());
        Assert.Equal("t.b", items[3].N());
        Assert.Equal("t.c", items[4].N());
    }

    [Fact]
    public void SimpleIdentifiers_NoneFilter_ReturnsNothing()
    {
        var text = "a + b * c - d / e";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None })];

        Assert.Empty(items);
    }

    [Fact]
    public void CompoundIdentifiers_NoneFilter_ReturnsNothing()
    {
        var text = "t.a + t.b + t.c - t.d / t.e";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None })];

        Assert.Empty(items);
    }

    [Fact]
    public void Select_SetOperation_WithDefinition_FromClauseItems()
    {
        var text = """
        SELECT a, b, c FROM table1
        UNION
        SELECT d, e, f FROM table2
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create([new Identifier("table2")], schema, NameHandling.None)] = ObjectType.Table,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Table, i.Type));
        Assert.Equal("table1", items[0].N());
        Assert.Equal("table2", items[1].N());
    }

    [Fact]
    public void Select_SetOperation_Unknown_SelectItems()
    {
        var text = """
        SELECT a, b, c FROM table1
        UNION
        SELECT d, e, f FROM table2
        """;
        var select = ParseSelect(text);

        // All items Unknown without definition: a, b, c, table1, d, e, f, table2
        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(8, items.Count);
    }

    [Fact]
    public void Select_ValuesQueryInFrom_NoSelectItems()
    {
        var text = "SELECT * FROM (VALUES (1, 'a'), (2, 'b'), (3, 'c'))";
        var select = ParseSelect(text);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Empty(items);
    }

    [Fact]
    public void Select_ValuesDirect_NoItems()
    {
        var text = "VALUES (1, 'a'), (2, 'b'), (3, 'c')";
        var select = ParseSelect(text);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Empty(items);
    }

    [Fact]
    public void Select_WithCte_WithDefinition_CteNameFilteredOut()
    {
        var text = """
        WITH cte AS (
            SELECT id, name FROM table1 WHERE status = 'active'
        )
        SELECT id, name FROM cte WHERE id > 100
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        // 'cte' is filtered out as a CTE name; only the real table inside the CTE body is returned.
        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("table1", item.N());
    }

    // ===== Tests for Definition-based type classification =====

    [Fact]
    public void Select_WithDefinition_TableClassifiedAsTable()
    {
        var text = "SELECT a FROM mytable";
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("mytable")], schema, NameHandling.None)] = ObjectType.Table,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", item.N());
        Assert.NotNull(item.ObjectHandle);
        Assert.Equal("mytable", item.ObjectHandle!.Value.Name);
        Assert.Equal("myschema", item.ObjectHandle!.Value.Schema);
    }

    [Fact]
    public void Select_WithDefinition_ViewClassifiedAsView()
    {
        var text = "SELECT a FROM myview";
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("myview")], schema, NameHandling.None)] = ObjectType.View,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.View, item.Type);
        Assert.Equal("myview", item.N());
        Assert.NotNull(item.ObjectHandle);
        Assert.Equal("myview", item.ObjectHandle!.Value.Name);
    }

    [Fact]
    public void Select_WithJoinsAndDefinition_MixedTableAndView()
    {
        var text = "SELECT t.a, v.b FROM mytable AS t JOIN myview AS v ON t.id = v.t_id";
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("mytable")], schema, NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create([new Identifier("myview")], schema, NameHandling.None)] = ObjectType.View,
        });

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table | ObjectNameFilters.View,
            Definition = provider,
            ActiveSchema = schema
        })];

        Assert.Equal(2, items.Count);
        Assert.Equal(ItemType.Table, items[0].Type);
        Assert.Equal("mytable", items[0].N());
        Assert.Equal(ItemType.View, items[1].Type);
        Assert.Equal("myview", items[1].N());
    }

    [Fact]
    public void Select_WithDefinition_UnknownObjectRemainsUnknown()
    {
        // Without the object in the definition, the FROM reference is Unknown.
        var text = "SELECT a FROM unknowntable";
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var provider = new SimpleObjectProvider([]);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Unknown,
            Definition = provider,
            ActiveSchema = schema
        })];

        // 'a' (SELECT, Unknown) and 'unknowntable' (FROM, Unknown) both pass.
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
    }

    // ===== Tests for PseudoTables-based column resolution =====

    [Fact]
    public void Select_WithPseudoTables_ColumnResolvedToTableColumn()
    {
        var text = """
        SELECT t.col1, t.col2
        FROM table1 AS t
        WHERE t.col1 > 5
        """;
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var tableIdentifier = new ObjectIdentifier("table1", schema);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("table1")], schema, NameHandling.None)] = ObjectType.Table,
        });
        var pseudoTables = new PseudoTableSet(
            schema,
            [new PseudoTable("table1", tableIdentifier, ["col1", "col2"], PseudoTableType.Table)]);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.TableColumn,
            Definition = provider,
            PseudoTables = pseudoTables
        })];

        // SELECT: t.col1, t.col2 as TableColumn; WHERE: t.col1 as TableColumn; FROM: table1 (Table, excluded).
        Assert.Equal(3, items.Count);
        Assert.All(items, i =>
        {
            Assert.Equal(ItemType.TableColumn, i.Type);
            Assert.True(i.IsColumn);
            Assert.NotNull(i.ColumnIdentifier);
            Assert.NotNull(i.ObjectHandle);
            Assert.Equal("table1", i.ObjectHandle.Value.Name);
            Assert.Equal("myschema", i.ObjectHandle.Value.Schema);
        });
        Assert.Equal("col1", items[0].ColumnIdentifier!.Name);
        Assert.Equal("col2", items[1].ColumnIdentifier!.Name);
        Assert.Equal("col1", items[2].ColumnIdentifier!.Name);
    }

    [Fact]
    public void Select_WithPseudoTables_ViewColumnResolvedToViewColumn()
    {
        var text = "SELECT v.col1 FROM myview AS v";
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var viewIdentifier = new ObjectIdentifier("myview", schema);
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("myview")], schema, NameHandling.None)] = ObjectType.View,
        });
        var pseudoTables = new PseudoTableSet(
            schema,
            [new PseudoTable("myview", viewIdentifier, ["col1"], PseudoTableType.View)]);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.ViewColumn,
            Definition = provider,
            PseudoTables = pseudoTables
        })];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.ViewColumn, item.Type);
        Assert.True(item.IsColumn);
        Assert.Equal("col1", item.ColumnIdentifier!.Name);
        Assert.Equal("myview", item.ObjectHandle!.Value.Name);
    }

    [Fact]
    public void Select_WithPseudoTables_UnknownAliasRemainsUnknown()
    {
        // Alias 'x' is not registered in the PseudoTableSet, so the column ref stays Unknown.
        var text = "SELECT x.col1 FROM sometable AS x";
        var select = ParseSelect(text);

        var catalog = new CatalogIdentifier("def");
        var schema = new SchemaIdentifier("myschema", catalog);
        var pseudoTables = new PseudoTableSet(schema, []);

        List<ItemRef> items = [.. select.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Unknown,
            PseudoTables = pseudoTables
        })];

        // x.col1 (Unknown) and sometable (Unknown, no definition) both pass.
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("sometable", items[0].N());
        Assert.False(items[1].IsColumn);
        Assert.Equal("x.col1", items[1].N());
    }

    // ===== Parse helpers =====

    public static Expression ParseExpression(string sql)
    {
        var tokens = new GenericLexer().Tokenize(sql);
        var state = new ParserState([.. tokens]);
        return new Parser().ExpressionParser.ParseExpr(state);
    }

    private static Select ParseSelect(string sql)
    {
        var tokens = new GenericLexer().Tokenize(sql);
        var state = new ParserState([.. tokens]);
        return (Select)new Parser().ParseStatement(state);
    }
}
