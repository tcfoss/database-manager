using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgSimpleStatementParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("COMMENT ON TABLE app.items IS 'table'", "app.items", Label = "Table comment")]
    [InlineData("COMMENT ON COLUMN app.items.id IS 'column'", "app.items", Label = "Column comment")]
    [InlineData("COMMENT ON FUNCTION app.f(INT) IS 'function'", "app.f", Label = "Function comment")]
    [InlineData("COMMENT ON TRIGGER audit ON app.items IS 'trigger'", "app.items", Label = "Trigger comment")]
    public void Comments_ReportTargetsAndRespectReferenceFilters(string sql, string expected)
    {
        var comment = Assert.IsType<CommentOn>(ParseStatement(sql));

        var references = comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }).OfType<ItemRef.ObjectNameRef>().ToArray();

        Assert.Equal(expected, Assert.Single(references).Name.ToSql());
        Assert.Empty(comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("COMMENT TABLE items IS 'text'", Label = "Missing ON")]
    [InlineData("COMMENT ON TABLE items 'text'", Label = "Missing IS")]
    [InlineData("COMMENT ON TABLE items IS 1", Label = "Numeric comment")]
    [InlineData("COMMENT ON TABLE items IS TRUE", Label = "TRUE comment")]
    [InlineData("COMMENT ON TABLE items IS FALSE", Label = "FALSE comment")]
    [InlineData("COMMENT ON TABLE items IS X'1234'", Label = "Bit string comment")]
    [InlineData("COMMENT ON TABLE items IS identifier", Label = "Identifier comment")]
    [InlineData("COMMENT ON TABLE items IS", Label = "Missing comment value")]
    [InlineData("COMMENT ON FUNCTION f(INT DEFAULT 1) IS 'text'", Label = "Function signature with default")]
    [InlineData("COMMENT ON TRIGGER app.audit ON items IS 'text'", Label = "Qualified trigger name")]
    [InlineData("COMMENT ON TRIGGER audit items IS 'text'", Label = "Missing trigger ON")]
    [InlineData("COMMENT ON TABLE items IS 'text' trailing", Label = "Trailing comment tokens")]
    public void InvalidComments_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void Comments_UnqualifiedColumnTargetsDoNotInventObjectReferences()
    {
        var target = new PgStatementCommentTarget.Column(new ObjectName(new Identifier("id")));
        var comment = new CommentOn(target, new Value.Null());

        Assert.Empty(comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Fact]
    public void Comments_UnsupportedTargetKindsAreRejected()
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements("COMMENT ON unsupported_kind items IS 'text'"));
    }

    [Theory]
    [InlineData("SELECT * FROM generate_series(1, 3)", Label = "Unaliased table function")]
    [InlineData("SELECT value FROM app.values_for($1) AS result (value)", Label = "Function with argument and column alias")]
    [InlineData("SELECT * FROM unnest(ARRAY[1, 2]) WITH ORDINALITY AS result (value, ordinal)", Label = "Function with ordinality")]
    [InlineData("SELECT * FROM LATERAL unnest(ARRAY[1, 2]) AS result (value)", Label = "Lateral function")]
    [InlineData("SELECT * FROM items CROSS JOIN LATERAL app.expand(items.value) AS result (value)", Label = "Cross join lateral function")]
    [InlineData("SELECT * FROM items LEFT OUTER JOIN LATERAL (SELECT items.id) AS result (value) ON TRUE", Label = "Left join lateral subquery")]
    [InlineData("SELECT * FROM ROWS FROM (app.first($1), app.second($2))", Label = "Rows from functions with arguments")]
    [InlineData("SELECT * FROM ROWS FROM (app.first(), app.second()) WITH ORDINALITY AS result (first, second, ordinal)", Label = "Rows from with ordinality")]
    [InlineData("SELECT * FROM LATERAL ROWS FROM (app.first(), app.second()) AS result (first, second)", Label = "Lateral rows from")]
    [InlineData("SELECT * FROM ROWS FROM (app.records() AS (id INT, label TEXT COLLATE app.locale)) AS result", Label = "Rows from with column definitions")]
    [InlineData("SELECT * FROM items AS result (value)", Label = "Table with column alias")]
    [InlineData("SELECT * FROM (SELECT 1 AS original) AS result (value)", Label = "Derived table with column alias")]
    [InlineData("SELECT * FROM (items INNER JOIN other ON TRUE) AS result (value)", Label = "Nested join with column alias")]
    public void FromRelations_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void FromFunction_PreservesLateralOrdinalityArgumentsAndAliasColumns()
    {
        var state = new ParserState(GetTokens("LATERAL app.expand($1) WITH ORDINALITY AS result (value, ordinal)"), 37);
        var relation = Assert.IsType<TableFactor.TableFunction>(new PgParser().SelectParser.ParseTableWithJoins(state).Relation);

        Assert.True(relation.IsLateral);
        Assert.True(relation.WithOrdinality);
        Assert.Equal("app.expand", relation.Function.Call.Name.ToSql());
        Assert.IsType<FunctionArguments.List>(relation.Function.Call.Arguments);
        Assert.Equal("result", relation.Alias!.Name);
        Assert.Equal(new[] { "value", "ordinal" }, relation.AliasColumns!.Select(column => column.Name));
        Assert.Equal(37, relation.Alias.Source!.Value.SourceId);
        Assert.IsType<Eof>(state.Peek());
    }

    [Fact]
    public void RowsFrom_PreservesPerFunctionColumnDefinitions()
    {
        var state = GetState("ROWS FROM (app.records() AS (id INT, label TEXT), app.other())");
        var relation = Assert.IsType<TableFactor.RowsFrom>(new PgParser().SelectParser.ParseTableWithJoins(state).Relation);

        Assert.Equal(2, relation.Functions.Count);
        Assert.Equal(new[] { "id", "label" }, relation.Functions[0].Columns!.Select(column => column.Name.Name));
        Assert.Null(relation.Functions[1].Columns);
        Assert.IsType<Eof>(state.Peek());
    }

    [Theory]
    [InlineData("SELECT result.value AS value FROM LATERAL app.expand(($1)) WITH ORDINALITY AS result (value, ordinal)", Label = "Normalize lateral function")]
    [InlineData("SELECT result.value AS value FROM LATERAL ROWS FROM (app.first(($1)) AS (value INT), app.second()) WITH ORDINALITY AS result (value, other, ordinal)", Label = "Normalize lateral rows from")]
    [InlineData("SELECT result.value AS value FROM LATERAL (SELECT $1 AS original) AS result (value)", Label = "Normalize lateral derived table")]
    public void FromRelations_NormalizeWithoutLosingComponents(string sql)
    {
        var config = PgTestConfig.GetTestConfig();
        var normalizer = new Normalizer(config, new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider()));
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Select normalized = normalizer.NormalizeSelect(statement);
        string normalizedSql = normalized.ToSql();

        Assert.Contains("LATERAL ", normalizedSql);
        Assert.Contains("AS \"result\" (\"value\"", normalizedSql);
        if (sql.Contains("WITH ORDINALITY", StringComparison.Ordinal))
        {
            Assert.Contains("WITH ORDINALITY", normalizedSql);
        }
        Assert.Equal(normalized, normalizer.NormalizeSelect(normalized));
    }

    [Fact]
    public void FromFunctions_ReportFunctionsAndArgumentReferencesWithoutInventingTables()
    {
        var statement = Assert.IsType<Select>(ParseStatement("SELECT result.value FROM app.items CROSS JOIN LATERAL app.expand(items.value) AS result (value)"));
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        ItemRef[] references = [.. statement.GetReferencedItems(context)];
        string[] objects = [.. references.OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Contains("app.items", objects);
        Assert.Contains("app.expand", objects);
        Assert.DoesNotContain("result", objects);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Fact]
    public void FromFunctions_PreserveAtomicBodyStatementBoundariesAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC SELECT * FROM app.first($1); SELECT * FROM ROWS FROM (app.second(), app.third()); END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Select[] statements = [.. body.Statements.OfType<Select>()];

        Assert.Equal(2, statements.Length);
        foreach (Select statement in statements)
        {
            Assert.NotNull(statement.Meta.EndPlusTerminatorLength);
            Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength.Value - 1]);
        }
        string[] functions = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.first", "app.second", "app.third" }, functions);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("INSERT INTO app.items DEFAULT VALUES", Label = "Default values without columns")]
    [InlineData("INSERT INTO app.items (id) DEFAULT VALUES", Label = "Default values with columns")]
    public void InsertDefaultValues_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Insert>(ParseStatement(sql));

        Assert.Null(statement.Source);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("INSERT INTO app.items DEFAULT VALUES RETURNING *", Label = "Insert defaults returning wildcard")]
    [InlineData("INSERT INTO app.items (id) VALUES (1), (2) RETURNING id AS item_id, app.render(id)", Label = "Insert rows returning alias and function")]
    [InlineData("INSERT INTO app.items SELECT id FROM app.source RETURNING items.*", Label = "Insert select returning qualified wildcard")]
    [InlineData("UPDATE app.items SET value = $1 RETURNING *", Label = "Update returning wildcard")]
    [InlineData("UPDATE app.items AS target SET value = source.value FROM app.source AS source, app.other AS other WHERE target.id = source.id RETURNING target.id AS item_id", Label = "Update from multiple aliased sources")]
    [InlineData("UPDATE app.items SET value = source.value FROM app.source AS source INNER JOIN app.other AS other ON source.id = other.id, app.third RETURNING items.*", Label = "Update from joined and comma sources")]
    [InlineData("DELETE FROM app.items USING app.source RETURNING items.*", Label = "Delete using source returning qualified wildcard")]
    [InlineData("DELETE FROM app.items AS target USING app.source AS source, app.other AS other WHERE target.id = source.id RETURNING target.id AS item_id, app.render(target.id)", Label = "Delete using multiple aliased sources")]
    [InlineData("DELETE FROM app.items USING app.source INNER JOIN app.other ON source.id = other.id RETURNING *", Label = "Delete using joined sources")]
    public void DmlReturningAndRelations_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("UPDATE app.items SET id = 1 FROM app.first, app.second WHERE app.check_id(id) RETURNING app.render(id)", Label = "Update source and function references")]
    [InlineData("DELETE FROM app.items USING app.first, app.second WHERE app.check_id(id) RETURNING app.render(id)", Label = "Delete source and function references")]
    public void DmlRelations_ReportSourcesAndReturningFunctions(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        string[] names = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Contains("app.items", names);
        Assert.Contains("app.first", names);
        Assert.Contains("app.second", names);
        Assert.Contains("app.check_id", names);
        Assert.Contains("app.render", names);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("INSERT INTO app.items OVERRIDING SYSTEM VALUE VALUES (1) RETURNING *", Label = "Override system value")]
    [InlineData("INSERT INTO app.items (id) OVERRIDING USER VALUE SELECT id FROM app.source", Label = "Override user value from select")]
    [InlineData("INSERT INTO app.items AS target (id) OVERRIDING SYSTEM VALUE VALUES (1) RETURNING target.id", Label = "Aliased target with system override")]
    [InlineData("INSERT INTO app.items AS target DEFAULT VALUES RETURNING target.*", Label = "Aliased target with default values")]
    public void InsertIdentityOverrideAndAlias_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Insert>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("INSERT INTO app.items DEFAULT VALUES ON CONFLICT DO NOTHING", Label = "Default values with untargeted do nothing")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO NOTHING RETURNING *", Label = "Inferred target with do nothing")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT items_pkey DO NOTHING", Label = "Constraint target with do nothing")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT items_pkey DO UPDATE SET id = excluded.id", Label = "Constraint target with do update")]
    [InlineData("INSERT INTO app.items AS target (id, value) OVERRIDING USER VALUE VALUES (1, 'value') ON CONFLICT (id) DO UPDATE SET value = excluded.value, id = DEFAULT WHERE target.value <> excluded.value RETURNING target.*", Label = "Aliased target with override and conditional update")]
    [InlineData("INSERT INTO app.items SELECT 1 ON CONFLICT (id) DO UPDATE SET id = excluded.id RETURNING id", Label = "Select literal with do update")]
    [InlineData("INSERT INTO app.items SELECT id FROM app.source ON CONFLICT DO NOTHING", Label = "Select source with do nothing")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id, (app.key(value))) WHERE app.active(id) DO UPDATE SET value = app.resolve(excluded.value) WHERE app.allowed(id) RETURNING app.render(id) AS result", Label = "Expression target and conditional update")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (name COLLATE \"C\" app.text_ops) DO NOTHING", Label = "Target collation and operator class")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ((app.key(name)) app.text_ops (option = 1)) DO NOTHING", Label = "Expression target with operator class parameters")]
    public void InsertOnConflict_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Insert>(ParseStatement(sql));

        Assert.NotNull(statement.OnConflict);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void InsertOnConflict_ReportsExpressionDependenciesWithoutInventingObjects()
    {
        const string sql = "INSERT INTO app.items VALUES (1) ON CONFLICT ((app.key(value))) WHERE app.active(id) DO UPDATE SET value = app.resolve(excluded.value) WHERE app.allowed(id) RETURNING app.render(id)";
        var statement = Assert.IsType<Insert>(ParseStatement(sql));
        string[] names = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Equal(new[] { "app.items", "app.key", "app.active", "app.resolve", "app.allowed", "app.render" }, names);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Fact]
    public void InsertOnConflict_PreservesTargetStructureAndIdentifierSources()
    {
        const string sql = "INSERT INTO app.items AS target (id) OVERRIDING USER VALUE VALUES (1) ON CONFLICT (id COLLATE app.locale app.int_ops) WHERE id > 0 DO UPDATE SET id = excluded.id WHERE target.id < excluded.id RETURNING target.id";
        var state = new ParserState(GetTokens(sql), 83);
        var statement = Assert.IsType<Insert>(new PgParser().ParseStatement(state));
        var action = Assert.IsType<PgOnConflict.DoUpdate>(statement.OnConflict);
        var target = Assert.IsType<PgConflictTarget.IndexInference>(action.Target);
        PgConflictIndexElement element = Assert.Single(target.Elements);

        Assert.Equal(PgIdentityOverride.UserValue, statement.Overriding);
        Assert.Equal("target", statement.Alias!.Name);
        Assert.Equal(83, statement.Alias.Source!.Value.SourceId);
        Assert.Equal(83, Assert.Single(statement.Columns!).Source!.Value.SourceId);
        Assert.IsType<Core.Expressions.Collate>(element.Expression);
        Assert.Equal("app.int_ops", element.OperatorClass!.ToSql());
        Assert.NotNull(target.Where);
        Assert.Single(action.Assignments);
        Assert.NotNull(action.Where);
        Assert.Single(statement.Returning!);
        Assert.IsType<Eof>(state.Peek());
    }

    [Fact]
    public void DmlComponents_PreserveAtomicBodyBoundariesAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC INSERT INTO app.items DEFAULT VALUES ON CONFLICT DO NOTHING RETURNING app.render(id); UPDATE app.items SET id = app.next(id) FROM app.source, app.other RETURNING id; DELETE FROM app.items USING app.source WHERE app.expired(id) RETURNING id; END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Statement[] statements = [.. body.Statements.Where(statement => statement is Insert or Update or Delete)];

        Assert.Equal(3, statements.Length);
        Assert.IsType<Insert>(statements[0]);
        Assert.IsType<Update>(statements[1]);
        Assert.IsType<Delete>(statements[2]);
        foreach (Statement statement in statements)
        {
            Assert.NotNull(statement.Meta.EndPlusTerminatorLength);
            Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength.Value - 1]);
        }
        string[] names = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Contains("app.items", names);
        Assert.Contains("app.source", names);
        Assert.Contains("app.other", names);
        Assert.Contains("app.render", names);
        Assert.Contains("app.next", names);
        Assert.Contains("app.expired", names);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("WITH result AS (SELECT 1 AS id) SELECT id FROM result", null, Label = "Implicit materialization")]
    [InlineData("WITH result AS MATERIALIZED (SELECT 1 AS id) SELECT id FROM result", true, Label = "Materialized CTE")]
    [InlineData("WITH result (id) AS NOT MATERIALIZED (SELECT 1) SELECT id FROM result", false, Label = "Not materialized CTE")]
    public void CteMaterialization_ParseAndRoundTrip(string sql, bool? materialized)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(materialized, Assert.Single(statement.CommonTableExpression!.Tables).Materialized);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("WITH changed AS (INSERT INTO app.items DEFAULT VALUES RETURNING id) SELECT id FROM changed", Label = "Insert CTE returning to select")]
    [InlineData("WITH changed (id) AS (UPDATE app.items SET id = 1 RETURNING id) SELECT id FROM changed", Label = "Update CTE with explicit columns")]
    [InlineData("WITH changed AS (DELETE FROM app.items RETURNING id) INSERT INTO app.archive SELECT id FROM changed", Label = "Delete CTE returning to insert")]
    [InlineData("WITH changed AS (DELETE FROM app.items) SELECT 1", Label = "Delete CTE without returning")]
    [InlineData("WITH source AS (SELECT id FROM app.items), changed AS (INSERT INTO app.archive SELECT id FROM source RETURNING id) SELECT id FROM changed", Label = "Insert CTE reading sibling CTE")]
    public void DataModifyingCtes_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items UNION ALL SELECT items.id FROM app.items INNER JOIN tree ON items.parent_id = tree.id) SEARCH DEPTH FIRST BY id SET position SELECT id, position FROM tree", Label = "Depth first recursive search")]
    [InlineData("WITH RECURSIVE tree (id) AS NOT MATERIALIZED (SELECT id FROM app.items) SEARCH BREADTH FIRST BY id SET position SELECT id FROM tree", Label = "Breadth first not materialized search")]
    [InlineData("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items) CYCLE id SET is_cycle USING path SELECT id, is_cycle, path FROM tree", Label = "Cycle with implicit marks")]
    [InlineData("WITH RECURSIVE tree (id, parent_id) AS (SELECT id, parent_id FROM app.items) SEARCH BREADTH FIRST BY id, parent_id SET position CYCLE id, parent_id SET is_cycle TO 'yes' DEFAULT 'no' USING path SELECT id FROM tree", Label = "Search and cycle with multiple columns")]
    [InlineData("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items) CYCLE id SET is_cycle TO TRUE DEFAULT FALSE USING path SELECT id FROM tree", Label = "Cycle with boolean marks")]
    [InlineData("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items) CYCLE id SET is_cycle TO -1 DEFAULT +0 USING path SELECT id FROM tree", Label = "Cycle with signed numeric marks")]
    [InlineData("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items) CYCLE id SET is_cycle TO NULL DEFAULT NULL USING path SELECT id FROM tree", Label = "Cycle with null marks")]
    [InlineData("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items) CYCLE id SET is_cycle TO INTERVAL '1 day' DEFAULT INTERVAL '0 days' USING path SELECT id FROM tree", Label = "Cycle with interval marks")]
    [InlineData("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items) CYCLE id SET is_cycle TO E'yes' DEFAULT $mark$no$mark$ USING path SELECT id FROM tree", Label = "Cycle with escape and dollar quoted marks")]
    [InlineData("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items) CYCLE id SET is_cycle TO TEXT 'yes' DEFAULT TEXT 'no' USING path, other AS (SELECT id FROM tree) SELECT id FROM other", Label = "Cycle with typed marks and sibling CTE")]
    public void CteSearchAndCycle_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void CteSearchAndCycle_ExposeGeneratedResultColumns()
    {
        var statement = Assert.IsType<Select>(ParseStatement("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items) SEARCH DEPTH FIRST BY id SET position CYCLE id SET is_cycle USING path SELECT id FROM tree"));
        CommonTableExpressionBody body = Assert.Single(statement.CommonTableExpression!.Tables);

        Assert.Equal(PgCteSearchOrder.DepthFirst, body.Search!.Order);
        Assert.Equal(new[] { "id", "position", "is_cycle", "path" }, body.ToPseudoTable().SelectableItems);
    }

    [Theory]
    [InlineData("WITH changed AS (INSERT INTO app.items VALUES (app.make()) RETURNING id AS item_id) SELECT item_id FROM changed", "app.items", "app.make", Label = "Insert CTE table and function references")]
    [InlineData("WITH changed AS (DELETE FROM app.items RETURNING id) INSERT INTO app.archive SELECT id FROM changed", "app.items", "app.archive", Label = "Delete CTE with insert consumer")]
    [InlineData("WITH changed AS (DELETE FROM app.items RETURNING id) UPDATE app.archive SET id = changed.id FROM changed", "app.items", "app.archive", Label = "Delete CTE with update consumer")]
    [InlineData("WITH changed AS (DELETE FROM app.items RETURNING id) DELETE FROM app.archive USING changed WHERE archive.id = changed.id", "app.items", "app.archive", Label = "Delete CTE with delete consumer")]
    public void DataModifyingCtes_ReportBodiesAndExcludeLocalRelations(string sql, string first, string second)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        string[] names = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Equal(new[] { first, second }, names);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("WITH RECURSIVE tree (id) AS (SELECT id FROM app.items UNION ALL SELECT items.id FROM app.items INNER JOIN tree ON TRUE) SELECT id FROM tree", "app.items", 2, Label = "Recursive self reference")]
    [InlineData("WITH items AS (SELECT id FROM items) SELECT id FROM items", "items", 1, Label = "Nonrecursive CTE matching table name")]
    [InlineData("WITH source AS (SELECT id FROM app.items), copied AS (SELECT id FROM source) SELECT id FROM copied", "app.items", 1, Label = "Sibling CTE reference")]
    public void CteReferences_RespectRecursiveAndSiblingScopes(string sql, string expected, int count)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));
        string[] names = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Equal(Enumerable.Repeat(expected, count), names);
    }

    [Theory]
    [InlineData("WITH changed AS MATERIALIZED (INSERT INTO app.items DEFAULT VALUES RETURNING id) SELECT id FROM changed", Label = "Normalize materialized insert CTE")]
    [InlineData("WITH changed AS (INSERT INTO app.items (id) VALUES ((1)) ON CONFLICT (id) DO UPDATE SET id = excluded.id RETURNING id) SELECT id FROM changed", Label = "Normalize insert CTE with conflict update")]
    [InlineData("WITH changed AS (UPDATE app.items SET id = (source.id) FROM app.source AS source WHERE items.id = source.id RETURNING id) SELECT id FROM changed", Label = "Normalize update CTE with source")]
    [InlineData("WITH changed AS (DELETE FROM app.items USING app.source WHERE items.id = source.id RETURNING id) SELECT id FROM changed", Label = "Normalize delete CTE with using")]
    [InlineData("WITH RECURSIVE tree (id) AS NOT MATERIALIZED (VALUES (1) UNION ALL SELECT id + 1 FROM tree WHERE id < 3) SEARCH DEPTH FIRST BY id SET position CYCLE id SET is_cycle TO 'yes' DEFAULT 'no' USING path SELECT id, position, is_cycle, path FROM tree", Label = "Normalize recursive CTE with search and cycle")]
    public void CteComponents_NormalizationPreservesBodiesAndClauses(string sql)
    {
        var config = PgTestConfig.GetTestConfig();
        var normalizer = new Normalizer(config, new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider()));
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Select normalized = normalizer.NormalizeSelect(statement);
        CommonTableExpressionBody originalBody = Assert.Single(statement.CommonTableExpression!.Tables);
        CommonTableExpressionBody normalizedBody = Assert.Single(normalized.CommonTableExpression!.Tables);

        Assert.Equal(originalBody.Query.GetType(), normalizedBody.Query.GetType());
        Assert.Equal(originalBody.Materialized, normalizedBody.Materialized);
        Assert.Equal(originalBody.Search?.Order, normalizedBody.Search?.Order);
        Assert.Equal(originalBody.Cycle?.Mark?.ToSql(), normalizedBody.Cycle?.Mark?.ToSql());
        Assert.Contains($"\"{originalBody.Name.Name}\"", normalized.ToSql());
        Assert.Equal(normalized, normalizer.NormalizeSelect(normalized));
    }

    [Fact]
    public void CteComponents_PreserveBodySpansCommentsAndIdentifierSources()
    {
        const string sql = "WITH RECURSIVE tree (id) AS ( /* lead */ SELECT id FROM app.items /* tail */ ) SEARCH DEPTH FIRST BY id SET position CYCLE id SET is_cycle USING path SELECT id FROM tree";
        var state = new ParserState(GetTokens(sql), 91);
        var statement = Assert.IsType<Select>(new PgParser().ParseStatement(state));
        CommonTableExpressionBody body = Assert.Single(statement.CommonTableExpression!.Tables);

        Assert.Equal(91, body.Name.Source!.Value.SourceId);
        Assert.Equal(91, body.Search!.SequenceColumn.Source!.Value.SourceId);
        Assert.Equal(91, body.Cycle!.MarkColumn.Source!.Value.SourceId);
        Assert.Equal(91, body.Cycle.PathColumn.Source!.Value.SourceId);
        Assert.Contains(body.Query.Meta.PreNonSql!, nonSql => nonSql is Core.Statements.Components.NonSql.BlockComment);
        Assert.Contains(body.Query.Meta.PostNonSql!, nonSql => nonSql is Core.Statements.Components.NonSql.BlockComment);
        Assert.Equal(sql.IndexOf(" /* lead */", StringComparison.Ordinal), body.Query.Meta.Start);
        Assert.Equal(sql.IndexOf(") SEARCH", StringComparison.Ordinal), body.Query.Meta.End);
        Assert.Equal(body.Query.Meta.End, body.Query.Meta.EndPlusTerminatorLength);
        Assert.IsType<Eof>(state.Peek());
    }

    [Fact]
    public void CteComponents_PreserveAtomicBodyTerminatorsAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC WITH changed AS (DELETE FROM app.items RETURNING id) INSERT INTO app.archive SELECT id FROM changed; WITH RECURSIVE tree (id) AS NOT MATERIALIZED (SELECT id FROM app.items) SEARCH BREADTH FIRST BY id SET position CYCLE id SET is_cycle USING path SELECT id FROM tree; END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Statement[] statements = [.. body.Statements.Where(statement => statement is Insert or Select)];

        Assert.Equal(2, statements.Length);
        foreach (Statement statement in statements)
        {
            Assert.NotNull(statement.Meta.EndPlusTerminatorLength);
            Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength.Value - 1]);
        }
        var insert = Assert.IsType<Insert>(statements[0]);
        Statement cteQuery = Assert.Single(insert.CommonTableExpression!.Tables).Query;
        Assert.IsType<Delete>(cteQuery);
        Assert.Equal(')', sql[cteQuery.Meta.End!.Value]);
        Assert.Equal(cteQuery.Meta.End, cteQuery.Meta.EndPlusTerminatorLength);
        string[] names = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.items", "app.archive", "app.items" }, names);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("WITH result AS (WITH source (id) AS (SELECT 1) SELECT id FROM source) SELECT id FROM result", Label = "Nested WITH select body")]
    [InlineData("WITH result (id) AS (WITH source AS (SELECT 1) VALUES (1)) SELECT id FROM result", Label = "Nested WITH values body")]
    [InlineData("WITH result (id) AS (SELECT 1) VALUES (1)", Label = "WITH values consumer")]
    public void CteQueries_ParseNestedWithAndValues(string sql)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("WITH result AS MATERIALIZED SELECT 1 SELECT 1", Label = "Missing CTE body parentheses")]
    [InlineData("WITH result AS NOT (SELECT 1) SELECT 1", Label = "Missing MATERIALIZED after NOT")]
    [InlineData("WITH result AS MATERIALIZED NOT MATERIALIZED (SELECT 1) SELECT 1", Label = "Duplicate materialization hints")]
    [InlineData("WITH result AS () SELECT 1", Label = "Empty CTE body")]
    [InlineData("WITH result AS (CREATE TABLE items (id INT)) SELECT 1", Label = "DDL CTE body")]
    [InlineData("WITH result AS (RETURN 1) SELECT 1", Label = "RETURN CTE body")]
    [InlineData("WITH result AS (SELECT 1; SELECT 2) SELECT 1", Label = "Multiple CTE body statements")]
    [InlineData("WITH result AS (INSERT INTO items VALUES (1);) SELECT 1", Label = "Terminated CTE body statement")]
    [InlineData("WITH result AS (SELECT 1) SEARCH FIRST BY id SET position SELECT 1", Label = "Missing search order")]
    [InlineData("WITH result AS (SELECT 1) SEARCH DEPTH BY id SET position SELECT 1", Label = "Missing FIRST in search")]
    [InlineData("WITH result AS (SELECT 1) SEARCH DEPTH FIRST id SET position SELECT 1", Label = "Missing BY in search")]
    [InlineData("WITH result AS (SELECT 1) SEARCH DEPTH FIRST BY id position SELECT 1", Label = "Missing SET in search")]
    [InlineData("WITH result AS (SELECT 1) SEARCH DEPTH FIRST BY id SET position SEARCH DEPTH FIRST BY id SET other SELECT 1", Label = "Duplicate search clauses")]
    [InlineData("WITH result AS (SELECT 1) CYCLE id SET is_cycle SELECT 1", Label = "Missing cycle USING")]
    [InlineData("WITH result AS (SELECT 1) CYCLE id USING path SELECT 1", Label = "Missing cycle SET")]
    [InlineData("WITH result AS (SELECT 1) CYCLE id SET is_cycle TO TRUE USING path SELECT 1", Label = "Missing cycle DEFAULT")]
    [InlineData("WITH result AS (SELECT 1) CYCLE id SET is_cycle DEFAULT FALSE USING path SELECT 1", Label = "Missing cycle TO")]
    [InlineData("WITH result AS (SELECT 1) CYCLE id SET is_cycle TO app.mark() DEFAULT FALSE USING path SELECT 1", Label = "Function call cycle mark")]
    [InlineData("WITH result AS (SELECT 1) CYCLE id SET is_cycle TO 1 + 2 DEFAULT 0 USING path SELECT 1", Label = "Arithmetic cycle mark")]
    [InlineData("WITH result AS (SELECT 1) CYCLE id SET is_cycle TO (TRUE) DEFAULT FALSE USING path SELECT 1", Label = "Parenthesized cycle mark")]
    [InlineData("WITH result AS (SELECT 1) CYCLE id SET is_cycle USING path SEARCH DEPTH FIRST BY id SET position SELECT 1", Label = "Search after cycle")]
    public void CteComponents_RejectMalformedSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("INSERT INTO items OUTPUT inserted.id VALUES (1)", Label = "SQL Server insert OUTPUT")]
    [InlineData("UPDATE items SET id = 1 OUTPUT inserted.id", Label = "SQL Server update OUTPUT")]
    [InlineData("DELETE FROM items OUTPUT deleted.id", Label = "SQL Server delete OUTPUT")]
    [InlineData("INSERT INTO items VALUES (1) ON DUPLICATE KEY UPDATE id = 1", Label = "MySQL duplicate key update")]
    public void DmlComponents_RejectOtherDialectClauses(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT", Label = "Incomplete conflict clause")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT () DO NOTHING", Label = "Empty conflict target")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id,) DO NOTHING", Label = "Trailing conflict target comma")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT DO NOTHING", Label = "Missing constraint name")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT app.items_pkey DO NOTHING", Label = "Qualified constraint name")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT DO UPDATE SET id = 1", Label = "Update without conflict target")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) UPDATE SET id = 1", Label = "Missing conflict DO")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO UPDATE", Label = "Missing conflict update SET")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO UPDATE SET", Label = "Missing conflict assignments")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) WHERE DO NOTHING", Label = "Missing conflict target predicate")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO NOTHING WHERE TRUE", Label = "Predicate after do nothing")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO UPDATE SET id = 1 WHERE RETURNING *", Label = "Missing conflict update predicate")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO NOTHING ON CONFLICT DO NOTHING", Label = "Duplicate conflict clauses")]
    [InlineData("INSERT INTO app.items OVERRIDING", Label = "Incomplete identity override")]
    [InlineData("INSERT INTO app.items OVERRIDING VALUE VALUES (1)", Label = "Missing identity override mode")]
    [InlineData("INSERT INTO app.items OVERRIDING SYSTEM VALUES (1)", Label = "Missing identity override VALUE")]
    [InlineData("INSERT INTO app.items AS", Label = "Missing insert target alias")]
    [InlineData("INSERT INTO app.items DEFAULT", Label = "Missing default VALUES")]
    [InlineData("INSERT INTO app.items DEFAULT VALUES RETURNING", Label = "Missing insert returning projection")]
    [InlineData("UPDATE app.items SET id = 1 FROM", Label = "Missing update source")]
    [InlineData("UPDATE app.items SET id = 1 FROM app.source,", Label = "Trailing update source comma")]
    [InlineData("UPDATE app.items SET id = 1 RETURNING", Label = "Missing update returning projection")]
    [InlineData("DELETE FROM app.items USING", Label = "Missing delete using source")]
    [InlineData("DELETE FROM app.items USING app.source,", Label = "Trailing delete using comma")]
    [InlineData("DELETE FROM app.items USING app.source RETURNING", Label = "Missing delete returning projection")]
    public void DmlReturningAndRelations_RejectMalformedComponents(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("SELECT * FROM ROWS FROM ()", Label = "Empty rows from list")]
    [InlineData("SELECT * FROM ROWS FROM (app.first(),)", Label = "Trailing rows from comma")]
    [InlineData("SELECT * FROM app.first() AS result ()", Label = "Empty function alias columns")]
    [InlineData("SELECT * FROM ROWS FROM (app.records() AS ())", Label = "Empty function column definitions")]
    [InlineData("SELECT * FROM LATERAL", Label = "Missing lateral relation")]
    public void FromRelations_RejectMalformedComponents(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
