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
    [InlineData("COMMENT ON TABLE app.items IS 'table'", "app.items")]
    [InlineData("COMMENT ON COLUMN app.items.id IS 'column'", "app.items")]
    [InlineData("COMMENT ON FUNCTION app.f(INT) IS 'function'", "app.f")]
    [InlineData("COMMENT ON TRIGGER audit ON app.items IS 'trigger'", "app.items")]
    public void Comments_ReportTargetsAndRespectReferenceFilters(string sql, string expected)
    {
        var comment = Assert.IsType<CommentOn>(ParseStatement(sql));

        var references = comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }).OfType<ItemRef.ObjectNameRef>().ToArray();

        Assert.Equal(expected, Assert.Single(references).Name.ToSql());
        Assert.Empty(comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("COMMENT TABLE items IS 'text'")]
    [InlineData("COMMENT ON TABLE items 'text'")]
    [InlineData("COMMENT ON TABLE items IS 1")]
    [InlineData("COMMENT ON TABLE items IS TRUE")]
    [InlineData("COMMENT ON TABLE items IS FALSE")]
    [InlineData("COMMENT ON TABLE items IS X'1234'")]
    [InlineData("COMMENT ON TABLE items IS identifier")]
    [InlineData("COMMENT ON TABLE items IS")]
    [InlineData("COMMENT ON FUNCTION f(INT DEFAULT 1) IS 'text'")]
    [InlineData("COMMENT ON TRIGGER app.audit ON items IS 'text'")]
    [InlineData("COMMENT ON TRIGGER audit items IS 'text'")]
    [InlineData("COMMENT ON TABLE items IS 'text' trailing")]
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
    [InlineData("SELECT * FROM generate_series(1, 3)")]
    [InlineData("SELECT value FROM app.values_for($1) AS result (value)")]
    [InlineData("SELECT * FROM unnest(ARRAY[1, 2]) WITH ORDINALITY AS result (value, ordinal)")]
    [InlineData("SELECT * FROM LATERAL unnest(ARRAY[1, 2]) AS result (value)")]
    [InlineData("SELECT * FROM items CROSS JOIN LATERAL app.expand(items.value) AS result (value)")]
    [InlineData("SELECT * FROM items LEFT OUTER JOIN LATERAL (SELECT items.id) AS result (value) ON TRUE")]
    [InlineData("SELECT * FROM ROWS FROM (app.first($1), app.second($2))")]
    [InlineData("SELECT * FROM ROWS FROM (app.first(), app.second()) WITH ORDINALITY AS result (first, second, ordinal)")]
    [InlineData("SELECT * FROM LATERAL ROWS FROM (app.first(), app.second()) AS result (first, second)")]
    [InlineData("SELECT * FROM ROWS FROM (app.records() AS (id INT, label TEXT COLLATE app.locale)) AS result")]
    [InlineData("SELECT * FROM items AS result (value)")]
    [InlineData("SELECT * FROM (SELECT 1 AS original) AS result (value)")]
    [InlineData("SELECT * FROM (items INNER JOIN other ON TRUE) AS result (value)")]
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
    [InlineData("SELECT result.value AS value FROM LATERAL app.expand(($1)) WITH ORDINALITY AS result (value, ordinal)")]
    [InlineData("SELECT result.value AS value FROM LATERAL ROWS FROM (app.first(($1)) AS (value INT), app.second()) WITH ORDINALITY AS result (value, other, ordinal)")]
    [InlineData("SELECT result.value AS value FROM LATERAL (SELECT $1 AS original) AS result (value)")]
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
    [InlineData("INSERT INTO app.items DEFAULT VALUES")]
    [InlineData("INSERT INTO app.items (id) DEFAULT VALUES")]
    public void InsertDefaultValues_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Insert>(ParseStatement(sql));

        Assert.Null(statement.Source);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("INSERT INTO app.items DEFAULT VALUES RETURNING *")]
    [InlineData("INSERT INTO app.items (id) VALUES (1), (2) RETURNING id AS item_id, app.render(id)")]
    [InlineData("INSERT INTO app.items SELECT id FROM app.source RETURNING items.*")]
    [InlineData("UPDATE app.items SET value = $1 RETURNING *")]
    [InlineData("UPDATE app.items AS target SET value = source.value FROM app.source AS source, app.other AS other WHERE target.id = source.id RETURNING target.id AS item_id")]
    [InlineData("UPDATE app.items SET value = source.value FROM app.source AS source INNER JOIN app.other AS other ON source.id = other.id, app.third RETURNING items.*")]
    [InlineData("DELETE FROM app.items USING app.source RETURNING items.*")]
    [InlineData("DELETE FROM app.items AS target USING app.source AS source, app.other AS other WHERE target.id = source.id RETURNING target.id AS item_id, app.render(target.id)")]
    [InlineData("DELETE FROM app.items USING app.source INNER JOIN app.other ON source.id = other.id RETURNING *")]
    public void DmlReturningAndRelations_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("UPDATE app.items SET id = 1 FROM app.first, app.second WHERE app.check_id(id) RETURNING app.render(id)")]
    [InlineData("DELETE FROM app.items USING app.first, app.second WHERE app.check_id(id) RETURNING app.render(id)")]
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
    [InlineData("INSERT INTO app.items OVERRIDING SYSTEM VALUE VALUES (1) RETURNING *")]
    [InlineData("INSERT INTO app.items (id) OVERRIDING USER VALUE SELECT id FROM app.source")]
    [InlineData("INSERT INTO app.items AS target (id) OVERRIDING SYSTEM VALUE VALUES (1) RETURNING target.id")]
    [InlineData("INSERT INTO app.items AS target DEFAULT VALUES RETURNING target.*")]
    public void InsertIdentityOverrideAndAlias_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Insert>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("INSERT INTO app.items DEFAULT VALUES ON CONFLICT DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO NOTHING RETURNING *")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT items_pkey DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT items_pkey DO UPDATE SET id = excluded.id")]
    [InlineData("INSERT INTO app.items AS target (id, value) OVERRIDING USER VALUE VALUES (1, 'value') ON CONFLICT (id) DO UPDATE SET value = excluded.value, id = DEFAULT WHERE target.value <> excluded.value RETURNING target.*")]
    [InlineData("INSERT INTO app.items SELECT 1 ON CONFLICT (id) DO UPDATE SET id = excluded.id RETURNING id")]
    [InlineData("INSERT INTO app.items SELECT id FROM app.source ON CONFLICT DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id, (app.key(value))) WHERE app.active(id) DO UPDATE SET value = app.resolve(excluded.value) WHERE app.allowed(id) RETURNING app.render(id) AS result")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (name COLLATE \"C\" app.text_ops) DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ((app.key(name)) app.text_ops (option = 1)) DO NOTHING")]
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
    [InlineData("INSERT INTO items OUTPUT inserted.id VALUES (1)")]
    [InlineData("UPDATE items SET id = 1 OUTPUT inserted.id")]
    [InlineData("DELETE FROM items OUTPUT deleted.id")]
    [InlineData("INSERT INTO items VALUES (1) ON DUPLICATE KEY UPDATE id = 1")]
    public void DmlComponents_RejectOtherDialectClauses(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT () DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id,) DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT app.items_pkey DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT DO UPDATE SET id = 1")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) UPDATE SET id = 1")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO UPDATE")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO UPDATE SET")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) WHERE DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO NOTHING WHERE TRUE")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO UPDATE SET id = 1 WHERE RETURNING *")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT (id) DO NOTHING ON CONFLICT DO NOTHING")]
    [InlineData("INSERT INTO app.items OVERRIDING")]
    [InlineData("INSERT INTO app.items OVERRIDING VALUE VALUES (1)")]
    [InlineData("INSERT INTO app.items OVERRIDING SYSTEM VALUES (1)")]
    [InlineData("INSERT INTO app.items AS")]
    [InlineData("INSERT INTO app.items DEFAULT")]
    [InlineData("INSERT INTO app.items DEFAULT VALUES RETURNING")]
    [InlineData("UPDATE app.items SET id = 1 FROM")]
    [InlineData("UPDATE app.items SET id = 1 FROM app.source,")]
    [InlineData("UPDATE app.items SET id = 1 RETURNING")]
    [InlineData("DELETE FROM app.items USING")]
    [InlineData("DELETE FROM app.items USING app.source,")]
    [InlineData("DELETE FROM app.items USING app.source RETURNING")]
    public void DmlReturningAndRelations_RejectMalformedComponents(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("SELECT * FROM ROWS FROM ()")]
    [InlineData("SELECT * FROM ROWS FROM (app.first(),)")]
    [InlineData("SELECT * FROM app.first() AS result ()")]
    [InlineData("SELECT * FROM ROWS FROM (app.records() AS ())")]
    [InlineData("SELECT * FROM LATERAL")]
    public void FromRelations_RejectMalformedComponents(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
