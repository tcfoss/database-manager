using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgViewParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("CREATE VIEW v AS SELECT 1")]
    [InlineData("CREATE VIEW v (id) AS VALUES (1)")]
    [InlineData("CREATE VIEW v AS WITH seed AS (SELECT 1 AS id) SELECT id FROM seed")]
    [InlineData("CREATE VIEW v AS (SELECT 1)")]
    public void OrdinaryViewSyntax_QueryBodiesRoundTrip(string sql)
    {
        var statement = Assert.IsType<CreateView>(ParseStatement(sql));

        Assert.False(statement.IfNotExists);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("CREATE VIEW IF NOT EXISTS v AS SELECT 1")]
    [InlineData("CREATE TEMP VIEW IF NOT EXISTS v AS SELECT 1")]
    [InlineData("CREATE OR REPLACE VIEW IF NOT EXISTS v AS SELECT 1")]
    [InlineData("CREATE OR ALTER VIEW v AS SELECT 1")]
    [InlineData("CREATE UNLOGGED VIEW v AS SELECT 1")]
    [InlineData("CREATE ALGORITHM = MERGE VIEW v AS SELECT 1")]
    [InlineData("CREATE DEFINER = CURRENT_USER VIEW v AS SELECT 1")]
    [InlineData("CREATE SQL SECURITY INVOKER VIEW v AS SELECT 1")]
    [InlineData("CREATE VIEW v AS SELECT 1 WITH DATA")]
    [InlineData("CREATE VIEW v AS SELECT 1 WITH NO DATA")]
    [InlineData("CREATE VIEW v AS SELECT 1 TABLESPACE fast")]
    [InlineData("CREATE VIEW v AS")]
    [InlineData("CREATE VIEW v (id INT) AS SELECT 1")]
    [InlineData("CREATE VIEW v AS INSERT INTO items VALUES (1)")]
    [InlineData("CREATE VIEW v AS UPDATE items SET id = 1")]
    [InlineData("CREATE VIEW v AS DELETE FROM items")]
    [InlineData("CREATE VIEW v AS WITH seed AS (SELECT 1) DELETE FROM items")]
    [InlineData("CREATE VIEW v AS CALL app.run()")]
    public void OrdinaryViewSyntax_RejectUnsupportedDeclarationsAndNonQueries(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("INSERT INTO items VALUES (1)")]
    [InlineData("UPDATE items SET id = 1")]
    [InlineData("DELETE FROM items")]
    [InlineData("CALL app.run()")]
    public void OrdinaryViewSyntax_NonQueryDiagnosticPointsToBody(string body)
    {
        string prefix = "/* lead */ CREATE VIEW app.v AS ";

        var exception = Assert.ThrowsAny<ParseException>(() => ParseStatements(prefix + body));

        var diagnostic = new ParseException.WithText(exception, "view.sql", prefix + body);
        Assert.Contains($"\n{new string(' ', prefix.Length)}^", diagnostic.Message);
    }

    [Theory]
    [InlineData("CREATE VIEW app.walk AS SELECT 1", false, null)]
    [InlineData("CREATE RECURSIVE VIEW app.walk (id) AS SELECT 1", true, null)]
    [InlineData("CREATE TEMP VIEW app.walk AS SELECT 1", false, "TEMP")]
    [InlineData("CREATE TEMPORARY VIEW app.walk AS SELECT 1", false, "TEMPORARY")]
    [InlineData("CREATE OR REPLACE TEMP RECURSIVE VIEW app.walk (id) AS SELECT 1", true, "TEMP")]
    [InlineData("CREATE TEMPORARY RECURSIVE VIEW walk (id) AS SELECT id FROM app.items UNION ALL SELECT id FROM walk", true, "TEMPORARY")]
    [InlineData("CREATE OR REPLACE TEMPORARY VIEW visible (id) WITH (security_barrier) AS SELECT id FROM app.items WITH CHECK OPTION", false, "TEMPORARY")]
    [InlineData("CREATE RECURSIVE VIEW \"App\".\"Walk\" (\"Id\") WITH (security_invoker = TRUE) AS SELECT 1 UNION ALL SELECT \"Id\" FROM \"Walk\"", true, null)]
    public void OrdinaryViewModifiers_RoundTripSqlAndJson(string sql, bool recursive, string? temporary)
    {
        var statement = Assert.IsType<CreateView>(ParseStatement(sql));

        Assert.Equal(recursive, statement.Recursive);
        Assert.Equal(temporary, statement.TemporaryLabel?.ToSql());
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("CREATE RECURSIVE VIEW app.walk (id) AS SELECT id FROM app.items UNION ALL SELECT id FROM walk", "app.items")]
    [InlineData("CREATE TEMP RECURSIVE VIEW app.walk (id) AS SELECT id FROM app.items UNION ALL SELECT id FROM walk", "app.items")]
    [InlineData("CREATE RECURSIVE VIEW walk (id) AS SELECT id FROM app.walk", "app.walk")]
    [InlineData("CREATE RECURSIVE VIEW app.walk (id) AS SELECT id FROM app.walk UNION ALL SELECT id FROM walk", "app.walk")]
    [InlineData("CREATE RECURSIVE VIEW \"Walk\" (id) AS SELECT id FROM app.items UNION ALL SELECT id FROM \"Walk\"", "app.items")]
    [InlineData("CREATE VIEW walk AS SELECT id FROM walk", "walk")]
    [InlineData("CREATE TEMP VIEW walk AS SELECT id FROM walk", "walk")]
    public void OrdinaryViewModifiers_ReportOnlyExternalObjectDependencies(string sql, string expected)
    {
        var statement = Assert.IsType<CreateView>(ParseStatement(sql));
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };

        var references = statement.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>().ToArray();

        Assert.Equal(expected, Assert.Single(references).Name.ToSql());
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Null(context.CurrentContext);
        Assert.Null(context.PseudoTables);
    }

    [Fact]
    public void OrdinaryViewModifiers_PreserveSourcesCommentsAndFollowingStatements()
    {
        const string sql = "/* lead */ CREATE OR REPLACE TEMP RECURSIVE VIEW app.walk (id) AS /* body */ SELECT id FROM app.items UNION ALL SELECT id FROM walk /* tail */; SELECT 2";
        var statements = ParseStatements(sql, sourceId: 157);
        var statement = Assert.IsType<CreateView>(statements[0]);

        Assert.Equal(2, statements.Count);
        Assert.True(statement.Recursive);
        Assert.Equal("TEMP", statement.TemporaryLabel!.ToSql());
        Assert.NotNull(statement.CreateOrLabel);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.All(statement.Name.Values, identifier => Assert.Equal(157, identifier.Source!.Value.SourceId));
        Assert.Equal(157, Assert.Single(statement.Columns!).Source!.Value.SourceId);
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.IsType<Select>(statements[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryViewModifiers_RestoreDependencyScopes(bool stopEarly)
    {
        var statement = Assert.IsType<CreateView>(ParseStatement("CREATE RECURSIVE VIEW walk (id) AS SELECT id FROM app.items UNION ALL SELECT id FROM walk"));
        var tables = new PseudoTableSet(null, []);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        using var scope = context.Enter(ReferencedItemsContext.WhereClause);

        var references = statement.GetReferencedItems(context);
        _ = stopEarly ? [.. references.Take(1)] : references.ToArray();

        Assert.Same(tables, context.PseudoTables);
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        Assert.Empty(tables.GetPseudoTables("walk"));
    }

    [Theory]
    [InlineData("TEMP")]
    [InlineData("TEMPORARY")]
    public void OrdinaryViewModifiers_TemporaryTablesPreserveSpelling(string modifier)
    {
        string sql = $"CREATE {modifier} TABLE items (id INT)";
        var table = Assert.IsType<CreateTable>(ParseStatement(sql));

        Assert.Equal(modifier, table.Temporary!.ToSql());
        Assert.Equal(sql, table.ToSql());
        Assert.Equal(table, ParseStatement(table.ToSql()));
        Statement[] statements = [table];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("CREATE RECURSIVE VIEW walk AS SELECT 1")]
    [InlineData("CREATE RECURSIVE VIEW walk () AS SELECT 1")]
    [InlineData("CREATE RECURSIVE VIEW walk (id,) AS SELECT 1")]
    [InlineData("CREATE RECURSIVE VIEW walk (id) AS SELECT 1 WITH CHECK OPTION")]
    [InlineData("CREATE RECURSIVE VIEW walk (id) AS SELECT 1 WITH LOCAL CHECK OPTION")]
    [InlineData("CREATE TEMP RECURSIVE VIEW walk (id) AS SELECT 1 WITH CASCADED CHECK OPTION")]
    [InlineData("CREATE RECURSIVE TEMP VIEW walk (id) AS SELECT 1")]
    [InlineData("CREATE TEMP TEMPORARY VIEW walk AS SELECT 1")]
    [InlineData("CREATE RECURSIVE RECURSIVE VIEW walk (id) AS SELECT 1")]
    [InlineData("CREATE RECURSIVE TABLE walk (id INT)")]
    [InlineData("CREATE RECURSIVE MATERIALIZED VIEW walk (id) AS SELECT 1")]
    [InlineData("CREATE TEMP MATERIALIZED VIEW walk AS SELECT 1")]
    public void OrdinaryViewModifiers_RejectMalformedAndUnsupportedCombinations(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE VIEW app.visible WITH (security_barrier = TRUE) AS SELECT * FROM app.items")]
    [InlineData("CREATE OR REPLACE VIEW app.visible (id) WITH (security_invoker = FALSE, check_option = 'local') AS SELECT id FROM app.items")]
    [InlineData("CREATE VIEW app.visible WITH (security_barrier) AS SELECT id FROM app.items WITH CASCADED CHECK OPTION")]
    public void OrdinaryViewOptions_RoundTripSqlAndJson(string sql)
    {
        var statement = Assert.IsType<CreateView>(ParseStatement(sql));

        Assert.NotEmpty(statement.PgOptions!);
        Assert.Null(statement.MsOptions);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("CREATE VIEW v WITH (security_barrier = true, security_invoker = false) AS SELECT 1", "CREATE VIEW v WITH (security_barrier = TRUE, security_invoker = FALSE) AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH (check_option = local) AS SELECT 1", "CREATE VIEW v WITH (check_option = 'local') AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH (custom.setting = -2, other = $tag$text$tag$) AS SELECT 1", "CREATE VIEW v WITH (custom.setting = -2, other = $tag$text$tag$) AS SELECT 1")]
    public void OrdinaryViewOptions_PreserveSupportedScalarForms(string sql, string expected)
    {
        var statement = Assert.IsType<CreateView>(ParseStatement(sql));

        Assert.Equal(expected, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void OrdinaryViewOptions_PreserveFieldsSourcesBoundariesAndQueryDependencies()
    {
        const string sql = "/* lead */ CREATE OR REPLACE VIEW app.visible (id) WITH (security_barrier = TRUE, security_invoker, check_option = 'local') AS /* body */ WITH seed AS (SELECT id FROM app.items) SELECT id FROM seed WITH LOCAL CHECK OPTION /* tail */; SELECT 2";
        var state = GetState(sql, 149);
        var parser = new PgParser();
        var statement = Assert.IsType<CreateView>(parser.ParseStatement(state));
        SqlValueList<PgStorageParameter> options = Assert.IsType<SqlValueList<PgStorageParameter>>(statement.PgOptions);

        Assert.Equal(new[] { "security_barrier", "security_invoker", "check_option" }, options.Select(option => option.Name.ToSql()));
        Assert.Equal("TRUE", Assert.IsType<Value.Boolean>(options[0].Value).ToSql());
        Assert.Null(options[1].Value);
        Assert.Equal("'local'", options[2].Value!.ToSql());
        Assert.Equal("LOCAL", Assert.IsType<ViewCheckOption.Specified>(statement.ViewCheckOption).Scope.ToSql());
        Assert.NotNull(statement.CreateOrLabel);
        Assert.Equal("id", Assert.Single(statement.Columns!).Name);
        foreach (PgStorageParameter option in options)
        {
            Identifier name = Assert.Single(option.Name.Values);
            Assert.Equal(149, name.Source!.Value.SourceId);
        }
        Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength!.Value - 1]);
        Assert.True(state.ConsumeTokenIs<Semicolon>());
        Assert.IsType<Select>(parser.ParseStatement(state));
        Assert.IsType<Eof>(state.Peek());
        string[] dependencies = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.items" }, dependencies);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Equal(statement.ToSql(), ParseStatement(statement.ToSql()).ToSql());
    }

    [Fact]
    public void OrdinaryViewOptions_OmittedOptionsRemainNull()
    {
        var statement = Assert.IsType<CreateView>(ParseStatement("CREATE VIEW v AS SELECT 1"));

        Assert.Null(statement.PgOptions);
        Assert.Null(statement.MsOptions);
    }

    [Theory]
    [InlineData("CREATE VIEW v WITH () AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH (security_barrier,) AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH (security_barrier =) AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH (security_barrier = 1 + 2) AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH (a.b.c = 1) AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH (security_barrier = TRUE AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH (security_barrier) WITH (security_invoker) AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH ENCRYPTION AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH SCHEMABINDING AS SELECT 1")]
    [InlineData("CREATE VIEW v WITH (security_barrier) AS SELECT 1 WITH NO DATA")]
    [InlineData("CREATE VIEW v USING heap AS SELECT 1")]
    [InlineData("CREATE VIEW v TABLESPACE fast AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW v AS SELECT 1 WITH LOCAL CHECK OPTION")]
    public void OrdinaryViewOptions_RejectMalformedAndOtherViewSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }


    [Theory]
    [InlineData("", null, false)]
    [InlineData(" WITH CHECK OPTION", "CASCADED", true)]
    [InlineData(" WITH CASCADED CHECK OPTION", "CASCADED", false)]
    [InlineData(" WITH LOCAL CHECK OPTION", "LOCAL", false)]
    public void OrdinaryViewCheckOptions_PreserveModeAndSpelling(string suffix, string? expectedMode, bool omittedMode)
    {
        string sql = $"CREATE VIEW v AS SELECT id FROM items{suffix}";
        var statement = Assert.IsType<CreateView>(ParseStatement(sql));

        if (expectedMode == null)
        {
            Assert.Null(statement.ViewCheckOption);
        }
        else if (omittedMode)
        {
            Assert.IsType<ViewCheckOption.Unspecified>(statement.ViewCheckOption);
        }
        else
        {
            Assert.Equal(expectedMode, Assert.IsType<ViewCheckOption.Specified>(statement.ViewCheckOption).Scope.ToSql());
        }
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("CREATE OR REPLACE VIEW app.visible (id) WITH (security_barrier = TRUE) AS SELECT id FROM app.items WITH CHECK OPTION")]
    [InlineData("CREATE VIEW v AS WITH seed AS (SELECT id FROM items) SELECT id FROM seed WITH CHECK OPTION")]
    [InlineData("CREATE VIEW v AS (SELECT id FROM items) WITH CHECK OPTION")]
    public void OrdinaryViewCheckOptions_ComposeWithExistingViewSyntax(string sql)
    {
        var statement = Assert.IsType<CreateView>(ParseStatement(sql));

        Assert.IsType<ViewCheckOption.Unspecified>(statement.ViewCheckOption);
        Assert.Equal("WITH CHECK OPTION", statement.ViewCheckOption.ToSql());
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Fact]
    public void OrdinaryViewCheckOptions_PreserveSourcesBoundariesAndQueryDependencies()
    {
        const string sql = "/* lead */ CREATE VIEW app.visible (id) AS SELECT id FROM app.items WITH CHECK OPTION /* tail */; SELECT 2";
        // var state = GetState(sql, sourceId: 151);
        // var parser = new PgParser();
        // var statement = Assert.IsType<CreateView>(ParseStatement(state));
        var statements = ParseStatements(sql, sourceId: 151);
        var statement = Assert.IsType<CreateView>(statements[0]);

        Assert.IsType<ViewCheckOption.Unspecified>(statement.ViewCheckOption);
        Assert.Equal(151, Assert.Single(statement.Columns!).Source!.Value.SourceId);
        Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength!.Value - 1]);
        Assert.Contains("WITH CHECK OPTION", sql[statement.Meta.Start!.Value..statement.Meta.End!.Value]);
        // Assert.True(state.ConsumeTokenIs<Semicolon>());
        // Assert.IsType<Select>(parser.ParseStatement(state));
        Assert.IsType<Select>(statements[1]);
        // Assert.IsType<Eof>(state.Peek());
        string[] dependencies = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.items" }, dependencies);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("CREATE VIEW v AS SELECT 1 WITH CHECK")]
    [InlineData("CREATE VIEW v AS SELECT 1 WITH OPTION")]
    [InlineData("CREATE VIEW v AS SELECT 1 WITH CHECK OPTION LOCAL")]
    [InlineData("CREATE VIEW v AS SELECT 1 WITH CHECK OPTION WITH CHECK OPTION")]
    [InlineData("CREATE VIEW v AS SELECT 1 WITH LOCAL CASCADED CHECK OPTION")]
    [InlineData("CREATE VIEW v AS SELECT 1 WITH DEFAULT CHECK OPTION")]
    [InlineData("CREATE VIEW v AS SELECT 1 WITH \"LOCAL\" CHECK OPTION")]
    [InlineData("CREATE MATERIALIZED VIEW v AS SELECT 1 WITH CHECK OPTION")]
    public void OrdinaryViewCheckOptions_RejectMalformedAndMaterializedForms(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("REFRESH MATERIALIZED VIEW app.summary", false, null)]
    [InlineData("REFRESH MATERIALIZED VIEW CONCURRENTLY app.summary", true, null)]
    [InlineData("REFRESH MATERIALIZED VIEW app.summary WITH DATA", false, true)]
    [InlineData("REFRESH MATERIALIZED VIEW app.summary WITH NO DATA", false, false)]
    [InlineData("REFRESH MATERIALIZED VIEW CONCURRENTLY app.summary WITH DATA", true, true)]
    [InlineData("REFRESH MATERIALIZED VIEW \"App\".\"Summary\" WITH NO DATA", false, false)]
    [InlineData("REFRESH MATERIALIZED VIEW \"summary; DROP TABLE items; --\"", false, null)]
    public void RefreshMaterializedViews_RoundTripSqlAndJson(string sql, bool concurrently, bool? withData)
    {
        var statement = Assert.IsType<RefreshMaterializedView>(ParseStatement(sql));

        Assert.Equal(concurrently, statement.Concurrently);
        Assert.Equal(withData, statement.WithData);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void RefreshMaterializedViews_ReportTargetAndRespectFilters()
    {
        var statement = Assert.IsType<RefreshMaterializedView>(ParseStatement("REFRESH MATERIALIZED VIEW summary"));
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        context.AddLocalVariable(new Identifier("summary"));
        using var scope = context.Enter(ReferencedItemsContext.SelectItem);

        var reference = Assert.IsType<ItemRef.ObjectNameRef>(Assert.Single(statement.GetReferencedItems(context)));

        Assert.Equal("summary", reference.Name.ToSql());
        Assert.Equal(ItemType.Unknown, reference.Type);
        Assert.Null(reference.ObjectHandle);
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        Assert.Single(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown }));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Table }));
    }

    [Fact]
    public void RefreshMaterializedViews_PreserveSourcesCommentsAndFollowingStatements()
    {
        const string sql = "/* lead */ REFRESH MATERIALIZED VIEW CONCURRENTLY app.summary WITH DATA /* tail */; SELECT 2;";
        var statements = ParseStatements(sql, sourceId: 137);
        var statement = Assert.IsType<RefreshMaterializedView>(statements[0]);

        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.All(statement.Name.Values, identifier => Assert.Equal(137, identifier.Source!.Value.SourceId));
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.Equal("SELECT 2", statements[1].ToSql());
    }

    [Fact]
    public void RefreshMaterializedViews_PreserveAtomicBodyBoundariesAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC REFRESH MATERIALIZED VIEW app.summary WITH NO DATA; SELECT 2; END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        var refresh = Assert.Single(body.Statements.OfType<RefreshMaterializedView>());

        Assert.False(refresh.WithData);
        Assert.Equal(';', sql[refresh.Meta.EndPlusTerminatorLength!.Value - 1]);
        Assert.Equal(refresh.Meta.End + 1, refresh.Meta.EndPlusTerminatorLength);
        var reference = Assert.IsType<ItemRef.ObjectNameRef>(Assert.Single(body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })));
        Assert.Equal("app.summary", reference.Name.ToSql());
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("REFRESH")]
    [InlineData("REFRESH VIEW summary")]
    [InlineData("REFRESH MATERIALIZED summary")]
    [InlineData("REFRESH MATERIALIZED VIEW")]
    [InlineData("REFRESH CONCURRENTLY MATERIALIZED VIEW summary")]
    [InlineData("REFRESH MATERIALIZED VIEW IF EXISTS summary")]
    [InlineData("REFRESH MATERIALIZED VIEW CONCURRENTLY CONCURRENTLY summary")]
    [InlineData("REFRESH MATERIALIZED VIEW summary CONCURRENTLY")]
    [InlineData("REFRESH MATERIALIZED VIEW summary, other")]
    [InlineData("REFRESH MATERIALIZED VIEW app.")]
    [InlineData("REFRESH MATERIALIZED VIEW 'summary'")]
    [InlineData("REFRESH MATERIALIZED VIEW summary WITH")]
    [InlineData("REFRESH MATERIALIZED VIEW summary WITH NO")]
    [InlineData("REFRESH MATERIALIZED VIEW summary WITH CHECK OPTION")]
    [InlineData("REFRESH MATERIALIZED VIEW summary WITH DATA WITH NO DATA")]
    [InlineData("REFRESH MATERIALIZED VIEW CONCURRENTLY summary WITH NO DATA")]
    [InlineData("REFRESH MATERIALIZED VIEW summary CASCADE")]
    public void RefreshMaterializedViews_RejectInvalidSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("DROP MATERIALIZED VIEW summary")]
    [InlineData("DROP MATERIALIZED VIEW IF EXISTS app.summary")]
    [InlineData("DROP MATERIALIZED VIEW app.summary, app.other")]
    [InlineData("DROP MATERIALIZED VIEW summary CASCADE")]
    [InlineData("DROP MATERIALIZED VIEW summary RESTRICT")]
    [InlineData("DROP MATERIALIZED VIEW IF EXISTS app.summary, app.other CASCADE")]
    [InlineData("DROP MATERIALIZED VIEW \"App\".\"Summary\", \"Other\" RESTRICT")]
    [InlineData("DROP MATERIALIZED VIEW \"summary; DROP TABLE items; --\"")]
    public void DropMaterializedViews_RoundTripSqlAndJson(string sql)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Equal(DroppableObject.MaterializedView, statement.ObjectType);
        Assert.False(statement.Temporary);
        Assert.Null(statement.OnObject);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(" CASCADE", "CASCADE")]
    [InlineData(" RESTRICT", "RESTRICT")]
    public void DropMaterializedViews_PreserveOptionalBehavior(string clause, string? behavior)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement($"DROP MATERIALIZED VIEW IF EXISTS app.summary, app.other{clause}"));

        Assert.True(statement.IfExists);
        Assert.Equal(new[] { "app.summary", "app.other" }, statement.Names.Select(name => name.ToSql()));
        Assert.Equal(behavior, statement.PgBehavior?.ToString());
    }

    [Fact]
    public void DropMaterializedViews_ReportTargetsWithoutOrdinaryViewResolution()
    {
        var statement = Assert.IsType<DropObject>(ParseStatement("DROP MATERIALIZED VIEW summary, app.other CASCADE"));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("summary")], schema, NameHandling.None)] = ObjectType.View
        });
        var context = new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.All,
            ActiveSchema = schema,
            Definition = provider
        };
        context.AddLocalVariable(new Identifier("summary"));
        using var scope = context.Enter(ReferencedItemsContext.SelectItem);

        ItemRef.ObjectNameRef[] references = [.. statement.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>()];

        Assert.Equal(new[] { "summary", "app.other" }, references.Select(reference => reference.Name.ToSql()));
        Assert.All(references, reference =>
        {
            Assert.Equal(ItemType.Unknown, reference.Type);
            Assert.Null(reference.ObjectHandle);
        });
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        Assert.Equal(2, statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown }).Count());
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.View }));
    }

    [Fact]
    public void DropMaterializedViews_PreserveSourcesCommentsAndFollowingStatements()
    {
        const string sql = "/* lead */ DROP MATERIALIZED VIEW IF EXISTS app.summary, app.other CASCADE /* tail */; SELECT 2;";
        var statements = new PgParser().Parse(GetState(sql, sourceId: 137), null);
        var statement = Assert.IsType<DropObject>(statements[0]);

        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.All(statement.Names.SelectMany(name => name.Values), identifier => Assert.Equal(137, identifier.Source!.Value.SourceId));
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.Equal("SELECT 2", statements[1].ToSql());
    }

    [Fact]
    public void DropMaterializedViews_PreserveAtomicBodyBoundariesAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC DROP MATERIALIZED VIEW app.summary, app.other RESTRICT; SELECT 2; END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        var statement = Assert.Single(body.Statements.OfType<DropObject>());

        Assert.Equal(DroppableObject.MaterializedView, statement.ObjectType);
        Assert.Equal(PgDropBehavior.Restrict, statement.PgBehavior);
        Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength!.Value - 1]);
        Assert.Equal(statement.Meta.End + 1, statement.Meta.EndPlusTerminatorLength);
        string[] targets = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.summary", "app.other" }, targets);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("DROP MATERIALIZED")]
    [InlineData("DROP MATERIALIZED summary")]
    [InlineData("DROP MATERIALIZED VIEW")]
    [InlineData("DROP MATERIALIZED VIEW IF EXISTS")]
    [InlineData("DROP MATERIALIZED VIEW IF NOT EXISTS summary")]
    [InlineData("DROP TEMPORARY MATERIALIZED VIEW summary")]
    [InlineData("DROP MATERIALIZED VIEW CONCURRENTLY summary")]
    [InlineData("DROP MATERIALIZED VIEW summary,")]
    [InlineData("DROP MATERIALIZED VIEW ()")]
    [InlineData("DROP MATERIALIZED VIEW 'summary'")]
    [InlineData("DROP MATERIALIZED VIEW app.")]
    [InlineData("DROP MATERIALIZED VIEW summary()")]
    [InlineData("DROP MATERIALIZED VIEW summary CASCADE RESTRICT")]
    [InlineData("DROP MATERIALIZED VIEW summary RESTRICT CASCADE")]
    [InlineData("DROP MATERIALIZED VIEW summary CASCADE CASCADE")]
    [InlineData("DROP MATERIALIZED VIEW summary CASCADE, other")]
    [InlineData("DROP MATERIALIZED VIEW summary ON items")]
    [InlineData("DROP MATERIALIZED VIEW summary WITH NO DATA")]
    public void DropMaterializedViews_RejectInvalidSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("ALTER MATERIALIZED VIEW app.summary RENAME TO summary_next")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS summary RENAME TO summary_next")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME COLUMN id TO item_id")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME id TO item_id")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS app.summary RENAME COLUMN id TO item_id")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET SCHEMA archive")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS app.summary SET SCHEMA archive")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS summary SET TABLESPACE fast")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET (fillfactor = 70)")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS app.summary RESET (fillfactor, toast.autovacuum_enabled)")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO owner_name")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS app.summary OWNER TO CURRENT_ROLE")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO CURRENT_USER")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO SESSION_USER")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO \"CURRENT_USER\"")]
    [InlineData("ALTER MATERIALIZED VIEW \"App\".\"Summary\" OWNER TO \"MixedCase\"")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO \"role; DROP TABLE items; --\"")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET ACCESS METHOD heap")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS app.summary SET ACCESS METHOD DEFAULT")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET ACCESS METHOD \"default\"")]
    [InlineData("ALTER MATERIALIZED VIEW summary CLUSTER ON summary_idx")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS app.summary SET WITHOUT CLUSTER")]
    [InlineData("ALTER MATERIALIZED VIEW \"App\".\"Summary\" SET ACCESS METHOD \"MixedCase\"")]
    [InlineData("ALTER MATERIALIZED VIEW summary CLUSTER ON \"index; DROP TABLE items; --\"")]
    [InlineData("ALTER MATERIALIZED VIEW \"App\".\"Summary\" RENAME COLUMN \"Id\" TO \"ItemId\"")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME TO \"next; DROP TABLE items; --\"")]
    [InlineData("ALTER MATERIALIZED VIEW summary DEPENDS ON EXTENSION ext")]
    [InlineData("ALTER MATERIALIZED VIEW app.summary NO DEPENDS ON EXTENSION \"MyExtension\"")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast, SET TABLESPACE slow")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER COLUMN id SET STATISTICS 100")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET STATISTICS DEFAULT")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER COLUMN body SET STORAGE EXTENDED")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER body SET COMPRESSION lz4")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER body SET COMPRESSION DEFAULT")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET (n_distinct = -0.5), ALTER COLUMN body RESET (n_distinct)")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET (fillfactor = 70), RESET (fillfactor)")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO owner_name, OWNER TO other_owner")]
    [InlineData("ALTER MATERIALIZED VIEW summary CLUSTER ON summary_idx, SET WITHOUT CLUSTER")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET ACCESS METHOD heap, SET (fillfactor = 70), OWNER TO owner_name")]
    public void AlterMaterializedViews_RoundTripSqlAndJson(string sql)
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("IF EXISTS ", true)]
    public void AlterMaterializedViews_PreserveIfExists(string prefix, bool expected)
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement($"ALTER MATERIALIZED VIEW {prefix}app.summary RENAME TO summary_next"));

        Assert.Equal(expected, statement.IfExists);
        Assert.Equal("app.summary", statement.Name.ToSql());
        Assert.Equal("summary_next", Assert.IsType<PgAlterMaterializedViewOperation.RenameTo>(Assert.Single(statement.Operations)).Name.Name);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("COLUMN ", true)]
    public void AlterMaterializedViews_PreserveColumnRenameFields(string prefix, bool expected)
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement($"ALTER MATERIALIZED VIEW summary RENAME {prefix}id TO item_id"));
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.RenameColumn>(Assert.Single(statement.Operations));

        Assert.Equal("id", operation.OldName.Name);
        Assert.Equal("item_id", operation.NewName.Name);
        Assert.Equal(expected, operation.UseColumnKeyword);
    }

    [Fact]
    public void AlterMaterializedViews_PreserveRelocationDestinations()
    {
        var schema = Assert.IsType<AlterMaterializedView>(ParseStatement("ALTER MATERIALIZED VIEW summary SET SCHEMA archive"));
        var tablespace = Assert.IsType<AlterMaterializedView>(ParseStatement("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast"));

        Assert.Equal("archive", Assert.IsType<PgAlterMaterializedViewOperation.SetSchema>(Assert.Single(schema.Operations)).Name.Name);
        Assert.Equal("fast", Assert.IsType<PgAlterMaterializedViewOperation.SetTablespace>(Assert.Single(tablespace.Operations)).Name.Name);
    }

    [Theory]
    [InlineData("RENAME TO summary_next")]
    [InlineData("RENAME COLUMN id TO item_id")]
    [InlineData("SET SCHEMA archive")]
    [InlineData("SET TABLESPACE fast")]
    [InlineData("SET (fillfactor = 70, toast.autovacuum_enabled = FALSE)")]
    [InlineData("RESET (fillfactor, toast.autovacuum_enabled)")]
    [InlineData("OWNER TO owner_name")]
    [InlineData("OWNER TO CURRENT_ROLE")]
    [InlineData("OWNER TO CURRENT_USER")]
    [InlineData("OWNER TO SESSION_USER")]
    [InlineData("OWNER TO \"CURRENT_USER\"")]
    [InlineData("SET ACCESS METHOD heap")]
    [InlineData("SET ACCESS METHOD DEFAULT")]
    [InlineData("CLUSTER ON summary_idx")]
    [InlineData("SET WITHOUT CLUSTER")]
    public void AlterMaterializedViews_ReportOnlyUnresolvedTargetAndRespectFilters(string clause)
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement($"ALTER MATERIALIZED VIEW summary {clause}"));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("summary")], schema, NameHandling.None)] = ObjectType.View
        });
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, ActiveSchema = schema, Definition = provider };
        context.AddLocalVariable(new Identifier("summary"));
        using var scope = context.Enter(ReferencedItemsContext.SelectItem);

        var reference = Assert.IsType<ItemRef.ObjectNameRef>(Assert.Single(statement.GetReferencedItems(context)));

        Assert.Equal("summary", reference.Name.ToSql());
        Assert.Equal(ItemType.Unknown, reference.Type);
        Assert.Null(reference.ObjectHandle);
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        Assert.Single(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown }));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.View }));
    }

    [Fact]
    public void AlterMaterializedViews_PreserveSourcesCommentsAndFollowingStatements()
    {
        const string sql = "/* lead */ ALTER MATERIALIZED VIEW IF EXISTS app.summary RENAME COLUMN id TO item_id /* tail */; SELECT 2;";
        var statements = new PgParser().Parse(GetState(sql, sourceId: 137), null);
        var statement = Assert.IsType<AlterMaterializedView>(statements[0]);
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.RenameColumn>(Assert.Single(statement.Operations));

        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.All(statement.Name.Values.Append(operation.OldName).Append(operation.NewName), identifier => Assert.Equal(137, identifier.Source!.Value.SourceId));
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.Equal("SELECT 2", statements[1].ToSql());
    }

    [Theory]
    [InlineData("SET SCHEMA archive", typeof(PgAlterMaterializedViewOperation.SetSchema))]
    [InlineData("OWNER TO owner_name", typeof(PgAlterMaterializedViewOperation.OwnerTo))]
    [InlineData("OWNER TO CURRENT_USER", typeof(PgAlterMaterializedViewOperation.OwnerTo))]
    [InlineData("SET ACCESS METHOD heap", typeof(PgAlterMaterializedViewOperation.SetAccessMethod))]
    [InlineData("SET ACCESS METHOD DEFAULT", typeof(PgAlterMaterializedViewOperation.SetDefaultAccessMethod))]
    [InlineData("CLUSTER ON summary_idx", typeof(PgAlterMaterializedViewOperation.ClusterOn))]
    [InlineData("SET WITHOUT CLUSTER", typeof(PgAlterMaterializedViewOperation.SetWithoutCluster))]
    public void AlterMaterializedViews_PreserveAtomicBodyBoundariesAndDependencies(string clause, Type operationType)
    {
        string sql = $"CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC ALTER MATERIALIZED VIEW app.summary {clause}; SELECT 2; END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        var statement = Assert.Single(body.Statements.OfType<AlterMaterializedView>());

        Assert.IsType(operationType, Assert.Single(statement.Operations));
        Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength!.Value - 1]);
        Assert.Equal(statement.Meta.End + 1, statement.Meta.EndPlusTerminatorLength);
        var reference = Assert.IsType<ItemRef.ObjectNameRef>(Assert.Single(body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })));
        Assert.Equal("app.summary", reference.Name.ToSql());
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("ALTER MATERIALIZED")]
    [InlineData("ALTER MATERIALIZED summary RENAME TO summary_next")]
    [InlineData("ALTER MATERIALIZED VIEW")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS")]
    [InlineData("ALTER MATERIALIZED VIEW IF NOT EXISTS summary RENAME TO summary_next")]
    [InlineData("ALTER TEMPORARY MATERIALIZED VIEW summary RENAME TO summary_next")]
    [InlineData("ALTER MATERIALIZED VIEW summary")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME TO")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME COLUMN id item_id")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME COLUMN id TO")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME TO app.summary_next")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME COLUMN app.id TO item_id")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME COLUMN id TO app.item_id")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET SCHEMA")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET SCHEMA app.archive")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE app.fast")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast NOWAIT")]
    [InlineData("ALTER MATERIALIZED VIEW 'summary' RENAME TO summary_next")]
    public void AlterMaterializedViews_RejectMalformedOrDeferredForms(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void MaterializedViewStorage_PreserveSetParametersAndOptionalValues()
    {
        const string sql = "ALTER MATERIALIZED VIEW IF EXISTS app.summary SET (fillfactor = 70, autovacuum_enabled, toast.autovacuum_enabled = FALSE)";
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement(sql));
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.SetStorageParameters>(Assert.Single(statement.Operations));

        Assert.True(statement.IfExists);
        Assert.Equal(new[] { "fillfactor", "autovacuum_enabled", "toast.autovacuum_enabled" }, operation.Parameters.Select(parameter => parameter.Name.ToSql()));
        Assert.Equal("70", Assert.IsType<Value.Number>(operation.Parameters[0].Value).Value);
        Assert.Null(operation.Parameters[1].Value);
        Assert.False(Assert.IsType<Value.Boolean>(operation.Parameters[2].Value).Value);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void MaterializedViewStorage_PreserveResetParameterOrderAndQuoting()
    {
        const string sql = "ALTER MATERIALIZED VIEW summary RESET (fillfactor, toast.autovacuum_enabled, \"MixedCase\")";
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement(sql));
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.ResetStorageParameters>(Assert.Single(statement.Operations));

        Assert.Equal(new[] { "fillfactor", "toast.autovacuum_enabled", "\"MixedCase\"" }, operation.Parameters.Select(name => name.ToSql()));
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("on", "'on'")]
    [InlineData("off", "'off'")]
    [InlineData("TRUE", "TRUE")]
    [InlineData("+70", "+70")]
    [InlineData("-1", "-1")]
    [InlineData("'it''s; opaque'", "'it''s; opaque'")]
    [InlineData("$$opaque; text$$", "$$opaque; text$$")]
    public void MaterializedViewStorage_PreserveScalarValues(string input, string expected)
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement($"ALTER MATERIALIZED VIEW summary SET (setting = {input})"));
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.SetStorageParameters>(Assert.Single(statement.Operations));

        Assert.Equal(expected, Assert.Single(operation.Parameters).Value!.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("SET (fillfactor = 70, toast.autovacuum_enabled = FALSE)", true)]
    [InlineData("RESET (fillfactor, toast.autovacuum_enabled)", false)]
    public void MaterializedViewStorage_PreserveSourcesCommentsAndFollowingStatements(string clause, bool isSet)
    {
        string sql = $"/* lead */ ALTER MATERIALIZED VIEW IF EXISTS app.summary {clause} /* tail */; SELECT 2;";
        var statements = new PgParser().Parse(GetState(sql, sourceId: 137), null);
        var statement = Assert.IsType<AlterMaterializedView>(statements[0]);
        ObjectName[] parameterNames = isSet
            ? [.. Assert.IsType<PgAlterMaterializedViewOperation.SetStorageParameters>(Assert.Single(statement.Operations)).Parameters.Select(parameter => parameter.Name)]
            : [.. Assert.IsType<PgAlterMaterializedViewOperation.ResetStorageParameters>(Assert.Single(statement.Operations)).Parameters];

        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.All(parameterNames.SelectMany(name => name.Values), identifier => Assert.Equal(137, identifier.Source!.Value.SourceId));
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.Equal("SELECT 2", statements[1].ToSql());
    }

    [Fact]
    public void MaterializedViewStorage_PreserveAtomicBodyBoundariesAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC ALTER MATERIALIZED VIEW app.summary SET (fillfactor = 70); ALTER MATERIALIZED VIEW app.summary RESET (fillfactor); SELECT 2; END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        AlterMaterializedView[] statements = [.. body.Statements.OfType<AlterMaterializedView>()];

        Assert.Equal(2, statements.Length);
        Assert.IsType<PgAlterMaterializedViewOperation.SetStorageParameters>(Assert.Single(statements[0].Operations));
        Assert.IsType<PgAlterMaterializedViewOperation.ResetStorageParameters>(Assert.Single(statements[1].Operations));
        Assert.All(statements, statement =>
        {
            Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength!.Value - 1]);
            Assert.Equal(statement.Meta.End + 1, statement.Meta.EndPlusTerminatorLength);
        });
        string[] targets = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.summary", "app.summary" }, targets);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("SET ()")]
    [InlineData("SET (fillfactor =)")]
    [InlineData("SET (fillfactor = 1 + 2)")]
    [InlineData("SET (fillfactor = (70))")]
    [InlineData("SET (fillfactor = 70,)")]
    [InlineData("SET (app.toast.setting = 1)")]
    [InlineData("SET fillfactor = 70")]
    [InlineData("SET (fillfactor = 70")]
    [InlineData("RESET ()")]
    [InlineData("RESET (fillfactor = 70)")]
    [InlineData("RESET (fillfactor,)")]
    [InlineData("RESET (app.toast.setting)")]
    [InlineData("RESET (\"\")")]
    [InlineData("RESET fillfactor")]
    [InlineData("RESET ALL")]
    [InlineData("RESET (fillfactor")]
    public void MaterializedViewStorage_RejectMalformedOrDeferredForms(string clause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"ALTER MATERIALIZED VIEW summary {clause}"));
    }

    [Theory]
    [InlineData("owner_name", "owner_name", ExtendedQuoteStyle.None)]
    [InlineData("\"MixedCase\"", "MixedCase", ExtendedQuoteStyle.Ansi)]
    [InlineData("\"CURRENT_USER\"", "CURRENT_USER", ExtendedQuoteStyle.Ansi)]
    public void MaterializedViewOwnership_PreserveNamedRoleAndSources(string role, string expectedName, ExtendedQuoteStyle expectedQuoteStyle)
    {
        string sql = $"/* lead */ ALTER MATERIALIZED VIEW IF EXISTS app.summary OWNER TO {role} /* tail */; SELECT 2;";
        var statements = ParseStatements(sql, sourceId: 137);
        var statement = Assert.IsType<AlterMaterializedView>(statements[0]);
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.OwnerTo>(Assert.Single(statement.Operations));
        var owner = Assert.IsType<Account.Identity>(operation.Owner);

        Assert.True(statement.IfExists);
        Assert.Equal(expectedName, owner.Name.Name);
        Assert.Equal(expectedQuoteStyle, owner.Name.QuoteStyle);
        Assert.All(statement.Name.Values, identifier => Assert.Equal(137, identifier.Source!.Value.SourceId));
        Assert.Equal(137, owner.Name.Source!.Value.SourceId);
        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.Equal("SELECT 2", statements[1].ToSql());
    }

    [Theory]
    [InlineData("CURRENT_ROLE", typeof(Account.CurrentRole), "CURRENT_ROLE")]
    [InlineData("CURRENT_USER", typeof(Account.CurrentUser), "CURRENT_USER")]
    [InlineData("SESSION_USER", typeof(Account.SessionUser), "SESSION_USER")]
    [InlineData("current_user", typeof(Account.CurrentUser), "CURRENT_USER")]
    public void MaterializedViewOwnership_PreserveSpecialRoleVariants(string role, Type roleType, string expectedSql)
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement($"ALTER MATERIALIZED VIEW summary OWNER TO {role}"));
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.OwnerTo>(Assert.Single(statement.Operations));

        Assert.False(statement.IfExists);
        Assert.IsType(roleType, operation.Owner);
        Assert.Equal(expectedSql, operation.Owner.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("OWNER")]
    [InlineData("OWNER TO")]
    [InlineData("OWNER owner_name")]
    [InlineData("OWNER TO app.owner_name")]
    [InlineData("OWNER TO 'owner_name'")]
    [InlineData("OWNER TO \"\"")]
    [InlineData("OWNER TO owner_name@host")]
    [InlineData("OWNER TO CURRENT_USER()")]
    [InlineData("OWNER TO CURRENT_ROLE()")]
    [InlineData("OWNER TO SESSION_USER()")]
    [InlineData("OWNER TO (owner_name)")]
    public void MaterializedViewOwnership_RejectMalformedOrDeferredForms(string clause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"ALTER MATERIALIZED VIEW summary {clause}"));
    }

    [Theory]
    [InlineData("SET ACCESS METHOD heap", "heap", QuoteStyle.None)]
    [InlineData("SET ACCESS METHOD \"MixedCase\"", "MixedCase", QuoteStyle.Ansi)]
    [InlineData("CLUSTER ON summary_idx", "summary_idx", QuoteStyle.None)]
    [InlineData("CLUSTER ON \"SummaryIdx\"", "SummaryIdx", QuoteStyle.Ansi)]
    public void MaterializedViewMaintenance_PreserveNamedFieldsSourcesAndBoundaries(string clause, string expectedName, QuoteStyle expectedQuoteStyle)
    {
        string sql = $"/* lead */ ALTER MATERIALIZED VIEW IF EXISTS app.summary {clause} /* tail */; SELECT 2;";
        var statements = new PgParser().Parse(GetState(sql, sourceId: 137), null);
        var statement = Assert.IsType<AlterMaterializedView>(statements[0]);
        Identifier name = Assert.Single(statement.Operations) is PgAlterMaterializedViewOperation.SetAccessMethod accessMethod
            ? accessMethod.Name
            : Assert.IsType<PgAlterMaterializedViewOperation.ClusterOn>(Assert.Single(statement.Operations)).Name;

        Assert.True(statement.IfExists);
        Assert.Equal(expectedName, name.Name);
        Assert.Equal(expectedQuoteStyle, name.QuoteStyle);
        Assert.All(statement.Name.Values.Append(name), identifier => Assert.Equal(137, identifier.Source!.Value.SourceId));
        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.Equal("SELECT 2", statements[1].ToSql());
    }

    [Fact]
    public void MaterializedViewMaintenance_DistinguishDefaultFromNamedAccessMethod()
    {
        var defaultMethod = Assert.IsType<AlterMaterializedView>(ParseStatement("ALTER MATERIALIZED VIEW IF EXISTS summary SET ACCESS METHOD DEFAULT"));
        var namedMethod = Assert.IsType<AlterMaterializedView>(ParseStatement("ALTER MATERIALIZED VIEW summary SET ACCESS METHOD \"default\""));

        Assert.True(defaultMethod.IfExists);
        Assert.IsType<PgAlterMaterializedViewOperation.SetDefaultAccessMethod>(Assert.Single(defaultMethod.Operations));
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.SetAccessMethod>(Assert.Single(namedMethod.Operations));
        Assert.Equal("default", operation.Name.Name);
        Assert.Equal(QuoteStyle.Ansi, operation.Name.QuoteStyle);
        Assert.NotEqual(defaultMethod.Operations, namedMethod.Operations);
    }

    [Theory]
    [InlineData("SET ACCESS")]
    [InlineData("SET ACCESS heap")]
    [InlineData("SET ACCESS METHOD")]
    [InlineData("SET ACCESS METHOD app.heap")]
    [InlineData("SET ACCESS METHOD 'heap'")]
    [InlineData("SET ACCESS METHOD \"\"")]
    [InlineData("SET ACCESS METHOD heap()")]
    [InlineData("SET ACCESS METHOD DEFAULT heap")]
    [InlineData("SET ACCESS METHOD heap NOWAIT")]
    [InlineData("SET WITHOUT")]
    [InlineData("SET WITHOUT OIDS")]
    [InlineData("SET WITHOUT CLUSTER summary_idx")]
    [InlineData("CLUSTER")]
    [InlineData("CLUSTER summary_idx")]
    [InlineData("CLUSTER ON")]
    [InlineData("CLUSTER ON app.summary_idx")]
    [InlineData("CLUSTER ON 'summary_idx'")]
    [InlineData("CLUSTER ON \"\"")]
    [InlineData("CLUSTER ON (summary_idx)")]
    [InlineData("CLUSTER ON summary_idx NOWAIT")]
    public void MaterializedViewMaintenance_RejectMalformedOrDeferredForms(string clause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"ALTER MATERIALIZED VIEW summary {clause}"));
    }

    [Theory]
    [InlineData("ALTER MATERIALIZED VIEW ALL IN TABLESPACE fast SET TABLESPACE slow")]
    [InlineData("ALTER MATERIALIZED VIEW ALL IN TABLESPACE \"OldSpace\" OWNED BY owner_name, CURRENT_ROLE, \"CURRENT_USER\" SET TABLESPACE \"NewSpace\" NOWAIT")]
    public void BulkMaterializedViews_RoundTripSqlAndJson(string sql)
    {
        var statement = Assert.IsType<AlterMaterializedViewsInTablespace>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Theory]
    [InlineData("ALTER COLUMN id SET STATISTICS -1", typeof(PgColumnConfigurationOperation.SetStatistics), true)]
    [InlineData("ALTER id SET STATISTICS DEFAULT", typeof(PgColumnConfigurationOperation.SetDefaultStatistics), false)]
    [InlineData("ALTER COLUMN id SET (n_distinct = -0.5, n_distinct_inherited = 100)", typeof(PgColumnConfigurationOperation.SetOptions), true)]
    [InlineData("ALTER id RESET (n_distinct, n_distinct_inherited)", typeof(PgColumnConfigurationOperation.ResetOptions), false)]
    [InlineData("ALTER id SET STORAGE PLAIN", typeof(PgColumnConfigurationOperation.SetStorage), false)]
    [InlineData("ALTER id SET STORAGE EXTERNAL", typeof(PgColumnConfigurationOperation.SetStorage), false)]
    [InlineData("ALTER id SET STORAGE EXTENDED", typeof(PgColumnConfigurationOperation.SetStorage), false)]
    [InlineData("ALTER id SET STORAGE MAIN", typeof(PgColumnConfigurationOperation.SetStorage), false)]
    [InlineData("ALTER id SET STORAGE DEFAULT", typeof(PgColumnConfigurationOperation.SetStorage), false)]
    [InlineData("ALTER id SET COMPRESSION lz4", typeof(PgColumnConfigurationOperation.SetCompression), false)]
    [InlineData("ALTER id SET COMPRESSION \"default\"", typeof(PgColumnConfigurationOperation.SetCompression), false)]
    [InlineData("ALTER id SET COMPRESSION DEFAULT", typeof(PgColumnConfigurationOperation.SetDefaultCompression), false)]
    public void MaterializedViewLifecycle_ColumnVariantsPreserveSourcesAndBoundaries(string clause, Type operationType, bool useColumnKeyword)
    {
        string sql = $"/* lead */ ALTER MATERIALIZED VIEW IF EXISTS app.summary {clause} /* tail */; SELECT 2;";
        var statements = new PgParser().Parse(GetState(sql, sourceId: 137), null);
        var statement = Assert.IsType<AlterMaterializedView>(statements[0]);
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.AlterColumn>(Assert.Single(statement.Operations));

        Assert.True(statement.IfExists);
        Assert.Equal("id", operation.Name.Name);
        Assert.Equal(137, operation.Name.Source!.Value.SourceId);
        Assert.Equal(useColumnKeyword, operation.UseColumnKeyword);
        Assert.IsType(operationType, operation.Operation);
        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.Equal("SELECT 2", statements[1].ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] serialized = [statement];
        Assert.Equal(serialized, Serialization.FromJson<Statement[]>(Serialization.ToJson(serialized)));
    }

    [Fact]
    public void MaterializedViewLifecycle_PreserveActionOrderAndColumnFields()
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement("ALTER MATERIALIZED VIEW summary ALTER COLUMN id SET STATISTICS 100, ALTER body SET STORAGE EXTENDED, SET (fillfactor = 70)"));

        Assert.Equal(3, statement.Operations.Count);
        var statistics = Assert.IsType<PgAlterMaterializedViewOperation.AlterColumn>(statement.Operations[0]);
        Assert.Equal(100, Assert.IsType<PgColumnConfigurationOperation.SetStatistics>(statistics.Operation).Target);
        Assert.Equal("id", statistics.Name.Name);
        var storage = Assert.IsType<PgAlterMaterializedViewOperation.AlterColumn>(statement.Operations[1]);
        Assert.Equal(PgColumnStorageMode.Extended, Assert.IsType<PgColumnConfigurationOperation.SetStorage>(storage.Operation).Mode);
        Assert.Equal("body", storage.Name.Name);
        Assert.IsType<PgAlterMaterializedViewOperation.SetStorageParameters>(statement.Operations[2]);
        var reference = Assert.IsType<ItemRef.ObjectNameRef>(Assert.Single(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })));
        Assert.Equal("summary", reference.Name.ToSql());
        Assert.Equal(ItemType.Unknown, reference.Type);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("NO ", true)]
    public void MaterializedViewLifecycle_PreserveExtensionDependencyFields(string prefix, bool expectedNo)
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement($"ALTER MATERIALIZED VIEW summary {prefix}DEPENDS ON EXTENSION \"MyExtension\""));
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.DependsOnExtension>(Assert.Single(statement.Operations));

        Assert.Equal(expectedNo, operation.No);
        Assert.Equal("MyExtension", operation.Name.Name);
        Assert.Equal(QuoteStyle.Ansi, operation.Name.QuoteStyle);
        Assert.Single(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Theory]
    [InlineData("", false, false)]
    [InlineData("", false, true)]
    [InlineData(" OWNED BY owner_name, CURRENT_USER", true, false)]
    [InlineData(" OWNED BY owner_name, CURRENT_USER", true, true)]
    public void BulkMaterializedViews_PreserveFieldsSourcesAndBoundaries(string ownerClause, bool hasOwners, bool useNowait)
    {
        string nowait = useNowait ? " NOWAIT" : "";
        string sql = $"/* lead */ ALTER MATERIALIZED VIEW ALL IN TABLESPACE \"OldSpace\"{ownerClause} SET TABLESPACE \"NewSpace\"{nowait} /* tail */; SELECT 2;";
        var statements = new PgParser().Parse(GetState(sql, sourceId: 137), null);
        var statement = Assert.IsType<AlterMaterializedViewsInTablespace>(statements[0]);

        Assert.Equal("OldSpace", statement.Tablespace.Name);
        Assert.Equal("NewSpace", statement.NewTablespace.Name);
        Assert.Equal(137, statement.Tablespace.Source!.Value.SourceId);
        Assert.Equal(137, statement.NewTablespace.Source!.Value.SourceId);
        Assert.Equal(useNowait, statement.Nowait);
        if (hasOwners)
        {
            Assert.Equal(2, statement.OwnedBy!.Count);
            Assert.Equal(137, Assert.IsType<Account.Identity>(statement.OwnedBy[0]).Name.Source!.Value.SourceId);
            Assert.IsType<Account.CurrentUser>(statement.OwnedBy[1]);
        }
        else
        {
            Assert.Null(statement.OwnedBy);
        }
        Assert.Equal(2, statements.Count);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.Equal("SELECT 2", statements[1].ToSql());
    }

    [Fact]
    public void MaterializedViewLifecycle_AtomicBodyPreservesBoundariesAndTargetOnlyDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC ALTER MATERIALIZED VIEW app.summary ALTER id SET STATISTICS 100, SET (fillfactor = 70); ALTER MATERIALIZED VIEW ALL IN TABLESPACE fast OWNED BY CURRENT_USER SET TABLESPACE slow NOWAIT; SELECT 2; END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);

        Statement[] commands = [.. body.Statements.Where(statement => statement is not InertOnly)];
        Assert.Equal(3, commands.Length);
        Assert.Equal(2, Assert.IsType<AlterMaterializedView>(commands[0]).Operations.Count);
        Assert.IsType<AlterMaterializedViewsInTablespace>(commands[1]);
        Assert.All(commands, statement => Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength!.Value - 1]));
        var reference = Assert.IsType<ItemRef.ObjectNameRef>(Assert.Single(body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })));
        Assert.Equal("app.summary", reference.Name.ToSql());
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("CREATE INDEX summary_idx ON app.summary (id)")]
    [InlineData("CREATE UNIQUE INDEX summary_idx ON app.summary (id)")]
    [InlineData("CREATE INDEX IF NOT EXISTS summary_idx ON app.summary (id) INCLUDE (label) WHERE id > 0")]
    public void MaterializedViewLifecycle_IndexDeclarationsReuseExistingAst(string sql)
    {
        var statement = Assert.IsType<CreateIndex>(ParseStatement(sql), exactMatch: false);

        Assert.Equal("app.summary", statement.TableName.ToSql());
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        var reference = Assert.IsType<ItemRef.ObjectNameRef>(Assert.Single(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })));
        Assert.Equal("app.summary", reference.Name.ToSql());
    }

    [Fact]
    public void MaterializedViewLifecycle_DeclarationIndexRefreshAndDropIndexWorkflow()
    {
        const string sql = "CREATE MATERIALIZED VIEW app.summary AS SELECT items.id FROM items WITH NO DATA; CREATE UNIQUE INDEX summary_idx ON app.summary (id); REFRESH MATERIALIZED VIEW app.summary; DROP INDEX summary_idx;";
        var statements = ParseStatements(sql);

        Assert.Collection(statements,
            statement => Assert.IsType<CreateMaterializedView>(statement),
            statement => Assert.IsType<CreateIndex.Unique>(statement),
            statement => Assert.IsType<RefreshMaterializedView>(statement),
            statement => Assert.IsType<DropObject>(statement));
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        Assert.All(statements, statement => Assert.Equal(statement, ParseStatement(statement.ToSql())));
    }

    [Theory]
    [InlineData("DROP INDEX summary_idx")]
    [InlineData("DROP INDEX IF EXISTS app.summary_idx")]
    public void DropIndex_PostgresRoundTripsWithoutOnTarget(string sql)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Equal(DroppableObject.Index, statement.ObjectType);
        Assert.Null(statement.OnObject);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Fact]
    public void DropIndex_PostgresRejectsOnTarget()
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements("DROP INDEX summary_idx ON app.summary"));
    }

    [Theory]
    [InlineData("ALTER COLUMN id")]
    [InlineData("ALTER COLUMN id SET STATISTICS 1.5")]
    [InlineData("ALTER COLUMN id SET STATISTICS 2147483648")]
    [InlineData("ALTER COLUMN id SET STORAGE invalid")]
    [InlineData("ALTER COLUMN id SET STORAGE 'PLAIN'")]
    [InlineData("ALTER COLUMN id SET STORAGE \"PLAIN\"")]
    [InlineData("ALTER COLUMN id SET COMPRESSION app.lz4")]
    [InlineData("ALTER COLUMN id SET COMPRESSION 'lz4'")]
    [InlineData("ALTER COLUMN id SET ()")]
    [InlineData("ALTER COLUMN id SET (n_distinct)")]
    [InlineData("ALTER COLUMN id SET (app.n_distinct = 1)")]
    [InlineData("ALTER COLUMN id SET (n_distinct = 1 + 2)")]
    [InlineData("ALTER COLUMN id RESET ()")]
    [InlineData("ALTER COLUMN id RESET (n_distinct = 1)")]
    [InlineData("ALTER COLUMN id RESET (app.n_distinct)")]
    [InlineData("ALTER COLUMN id SET NOT NULL")]
    [InlineData("DEPENDS EXTENSION ext")]
    [InlineData("NO DEPENDS ON EXTENSION")]
    [InlineData("DEPENDS ON EXTENSION app.ext")]
    [InlineData("DEPENDS ON EXTENSION 'ext'")]
    [InlineData("SET (fillfactor = 70),")]
    [InlineData("SET (fillfactor = 70), RENAME TO other")]
    [InlineData("RENAME TO other, SET (fillfactor = 70)")]
    [InlineData("SET SCHEMA archive, SET TABLESPACE fast")]
    [InlineData("SET TABLESPACE fast, SET SCHEMA archive")]
    [InlineData("DEPENDS ON EXTENSION ext, SET (fillfactor = 70)")]
    [InlineData("SET (fillfactor = 70), NO DEPENDS ON EXTENSION ext")]
    public void MaterializedViewLifecycle_RejectInvalidColumnOrStandaloneActions(string clause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"ALTER MATERIALIZED VIEW summary {clause}"));
    }

    [Theory]
    [InlineData("IF EXISTS summary DEPENDS ON EXTENSION ext")]
    [InlineData("IF EXISTS summary NO DEPENDS ON EXTENSION ext")]
    [InlineData("ALL IN TABLESPACE")]
    [InlineData("ALL IN TABLESPACE fast")]
    [InlineData("ALL IN TABLESPACE app.fast SET TABLESPACE slow")]
    [InlineData("ALL IN TABLESPACE fast SET TABLESPACE app.slow")]
    [InlineData("ALL IN TABLESPACE fast OWNED BY SET TABLESPACE slow")]
    [InlineData("ALL IN TABLESPACE fast OWNED BY owner_name, SET TABLESPACE slow")]
    [InlineData("ALL IN TABLESPACE fast OWNED BY 'owner_name' SET TABLESPACE slow")]
    [InlineData("ALL IN TABLESPACE fast OWNED BY owner_name@host SET TABLESPACE slow")]
    [InlineData("ALL IN TABLESPACE fast SET TABLESPACE slow NOWAIT NOWAIT")]
    [InlineData("ALL IN TABLESPACE fast SET TABLESPACE slow, SET TABLESPACE other")]
    public void BulkMaterializedViews_RejectMalformedForms(string clause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"ALTER MATERIALIZED VIEW {clause}"));
    }
}
