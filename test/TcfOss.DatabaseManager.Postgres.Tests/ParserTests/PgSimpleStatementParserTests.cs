using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
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

    [Fact]
    public void SelectTails_PreserveLockTargetSourcesAndAtomicBodyBoundaries()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC SELECT 1 FROM app.items AS i ORDER BY app.sort_value() LIMIT app.count_rows() OFFSET app.skip_rows() FOR NO KEY UPDATE OF i NOWAIT; SELECT 2 AS value FETCH NEXT ROW ONLY; END";
        var state = new ParserState(GetTokens(sql), 73);
        var procedure = Assert.IsType<CreateProcedure>(new PgParser().ParseStatement(state));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Select[] selects = [.. body.Statements.OfType<Select>()];

        Assert.Equal(2, selects.Length);
        Identifier target = Assert.Single(Assert.Single(selects[0].RowLocks!).Of!);
        Assert.Equal(73, target.Source!.Value.SourceId);
        Assert.Equal("i", target.Name);
        foreach (Select select in selects)
        {
            Assert.NotNull(select.Meta.Start);
            Assert.NotNull(select.Meta.End);
            Assert.Equal(';', sql[select.Meta.EndPlusTerminatorLength!.Value - 1]);
            Assert.Equal(select.Meta.End + 1, select.Meta.EndPlusTerminatorLength);
        }
        Assert.Contains("FOR NO KEY UPDATE OF i NOWAIT", sql[selects[0].Meta.Start!.Value..selects[0].Meta.End!.Value]);
        Assert.Contains("FETCH NEXT ROW ONLY", sql[selects[1].Meta.Start!.Value..selects[1].Meta.End!.Value]);
        string[] names = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.items", "app.sort_value", "app.count_rows", "app.skip_rows" }, names);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
        Assert.IsType<Eof>(state.Peek());
    }

    [Theory]
    [InlineData("CALL app.run()", Label = "No procedure arguments")]
    [InlineData("CALL app.run(1, NULL, 'text')", Label = "Positional procedure arguments")]
    [InlineData("CALL app.run($1, app.compute($2))", Label = "Parameter and nested function")]
    [InlineData("CALL app.run(value => 1)", Label = "Named procedure argument")]
    [InlineData("CALL app.run(value := 1)", Label = "Legacy named procedure argument")]
    [InlineData("CALL \"App\".\"Run\"(1)", Label = "Quoted procedure identity")]
    [InlineData("CALL lower('text')", Label = "Procedure with built-in function spelling")]
    [InlineData("CALL app.run(1 = 1)", Label = "Boolean argument is not named")]
    [InlineData("CALL app.run(VARIADIC ARRAY[1, 2])", Label = "Variadic array argument")]
    [InlineData("CALL app.run(1, VARIADIC app.values_for($1))", Label = "Variadic function argument")]
    [InlineData("CALL app.run(VARIADIC values_arg => ARRAY[1, 2])", Label = "Named variadic argument")]
    public void Call_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Call>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void Call_EqualityArgumentsArePositionalExpressions()
    {
        var call = Assert.IsType<Call>(ParseStatement("CALL app.run(1 = 1, app.compute(value = 2))"));
        var invocation = Assert.IsType<FunctionCall>(call.Expression);
        var arguments = Assert.IsType<FunctionArguments.List>(invocation.Arguments);
        var argument = Assert.IsType<FunctionArgument.Unnamed>(arguments.Arguments![0]);
        var expression = Assert.IsType<FunctionArgumentExpression.FunctionExpression>(argument.Argument);

        Assert.IsType<Core.Expressions.BinaryOperator>(expression.Expression);
        string[] names = [.. call.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.run", "app.compute" }, names);
        var nestedArgument = Assert.IsType<FunctionArgument.Unnamed>(arguments.Arguments[1]);
        var nestedCall = Assert.IsType<FunctionCall>(Assert.IsType<FunctionArgumentExpression.FunctionExpression>(nestedArgument.Argument).Expression);
        var nestedArguments = Assert.IsType<FunctionArguments.List>(nestedCall.Arguments);
        var predicate = Assert.IsType<FunctionArgument.Unnamed>(Assert.Single(nestedArguments.Arguments!));
        Assert.IsType<Core.Expressions.BinaryOperator>(Assert.IsType<FunctionArgumentExpression.FunctionExpression>(predicate.Argument).Expression);
    }

    [Fact]
    public void Call_ReferenceFiltersDistinguishProcedureTargetAndArgumentFunctions()
    {
        var call = Assert.IsType<Call>(ParseStatement("CALL lower(app.compute(LOWER('TEXT')))"));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("lower")], schema, NameHandling.None)] = ObjectType.Procedure,
            [ObjectHandle.Create([new Identifier("compute")], schema, NameHandling.None)] = ObjectType.Function
        });
        var context = new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.All,
            ActiveSchema = schema,
            Definition = provider,
            FunctionNameProvider = new FunctionNameProvider()
        };

        ItemRef.ObjectNameRef[] references = [.. call.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>()];

        Assert.Equal(new[] { "lower", "app.compute", "LOWER" }, references.Select(reference => reference.Name.ToSql()));
        Assert.Equal(new[] { ItemType.Procedure, ItemType.Function, ItemType.BuiltInFunction }, references.Select(reference => reference.Type));
        Assert.NotNull(references[0].ObjectHandle);
        Assert.Empty(call.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        ItemRef unresolved = Assert.Single(Assert.IsType<Call>(ParseStatement("CALL lower()"))
            .GetReferencedItems(new ReferencedItemsManager
            {
                Filters = ObjectNameFilters.Unknown,
                FunctionNameProvider = new FunctionNameProvider()
            }));
        Assert.Equal(ItemType.Unknown, unresolved.Type);
        ItemRef procedure = Assert.Single(call.GetReferencedItems(new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Procedure,
            ActiveSchema = schema,
            Definition = provider,
            FunctionNameProvider = new FunctionNameProvider()
        }));
        Assert.Equal(ItemType.Procedure, procedure.Type);
    }

    [Theory]
    [InlineData(false, Label = "Call argument normalization without flattening")]
    [InlineData(true, Label = "Call argument normalization with flattening")]
    public void Call_ArgumentsUseExistingExpressionNormalization(bool flatten)
    {
        var call = Assert.IsType<Call>(ParseStatement("CALL app.run((1 + 2), value := (app.compute((3 + 4))))"));
        var normalizer = new Normalizer(PgTestConfig.GetTestConfig(), new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider()));

        Expression normalized = flatten
            ? normalizer.NormalizeExpression(call.Expression)
            : normalizer.NormalizeExpressionNoFlatten(call.Expression);
        var arguments = Assert.IsType<FunctionArguments.List>(Assert.IsType<FunctionCall>(normalized).Arguments);
        var positional = Assert.IsType<FunctionArgument.Unnamed>(arguments.Arguments![0]);
        var named = Assert.IsType<FunctionArgument.Named>(arguments.Arguments[1]);

        Assert.Equal(!flatten, Assert.IsType<FunctionArgumentExpression.FunctionExpression>(positional.Argument).Expression is Nested);
        Assert.Equal(!flatten, Assert.IsType<FunctionArgumentExpression.FunctionExpression>(named.Argument).Expression is Nested);
        Assert.Equal(QuoteStyle.Ansi, named.Name.QuoteStyle);
        Assert.Equal(normalized, flatten ? normalizer.NormalizeExpression(normalized) : normalizer.NormalizeExpressionNoFlatten(normalized));
    }

    [Theory]
    [InlineData(false, Label = "Variadic argument without flattening")]
    [InlineData(true, Label = "Variadic argument with flattening")]
    public void Call_VariadicArgumentsNormalizeAndCollectDependencies(bool flatten)
    {
        var call = Assert.IsType<Call>(ParseStatement("CALL app.run(VARIADIC values_arg => (app.values_for((1 + 2))))"));
        var normalizer = new Normalizer(PgTestConfig.GetTestConfig(), new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider()));

        Expression normalized = flatten
            ? normalizer.NormalizeExpression(call.Expression)
            : normalizer.NormalizeExpressionNoFlatten(call.Expression);
        var arguments = Assert.IsType<FunctionArguments.List>(Assert.IsType<FunctionCall>(normalized).Arguments);
        var variadic = Assert.IsType<FunctionArgument.Variadic>(Assert.Single(arguments.Arguments!));
        var named = Assert.IsType<FunctionArgument.Named>(variadic.Argument);

        Assert.Equal(QuoteStyle.Ansi, named.Name.QuoteStyle);
        Assert.Equal(!flatten, Assert.IsType<FunctionArgumentExpression.FunctionExpression>(named.Argument).Expression is Nested);
        Assert.Equal(normalized, flatten ? normalizer.NormalizeExpression(normalized) : normalizer.NormalizeExpressionNoFlatten(normalized));
        string[] names = [.. call.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.run", "app.values_for" }, names);
    }

    [Fact]
    public void Call_PreservesAtomicBodySourcesCommentsTerminatorsAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC /* lead */ CALL app.first(value => app.compute($1)) /* tail */; SELECT 1; CALL app.second(NULL); END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(GetState(sql, 79)));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Call[] calls = [.. body.Statements.OfType<Call>()];

        Assert.Equal(2, calls.Length);
        var invocation = Assert.IsType<FunctionCall>(calls[0].Expression);
        Assert.All(invocation.Name.Values, identifier => Assert.Equal(79, identifier.Source!.Value.SourceId));
        var argument = Assert.IsType<FunctionArgument.Named>(Assert.Single(Assert.IsType<FunctionArguments.List>(invocation.Arguments).Arguments!));
        Assert.Equal(79, argument.Name.Source!.Value.SourceId);
        Assert.Contains(calls[0].Meta.PreNonSql!, nonSql => nonSql is Core.Statements.Components.NonSql.BlockComment);
        Assert.Contains(calls[0].Meta.PostNonSql!, nonSql => nonSql is Core.Statements.Components.NonSql.BlockComment);
        foreach (Call call in calls)
        {
            Assert.NotNull(call.Meta.Start);
            Assert.NotNull(call.Meta.End);
            Assert.Equal(';', sql[call.Meta.EndPlusTerminatorLength!.Value - 1]);
            Assert.Equal(call.Meta.End + 1, call.Meta.EndPlusTerminatorLength);
            Assert.Contains("CALL app.", sql[call.Meta.Start!.Value..call.Meta.End!.Value]);
        }
        string[] names = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.first", "app.compute", "app.second" }, names);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("CALL", Label = "Missing procedure name")]
    [InlineData("CALL app.run", Label = "Missing invocation parentheses")]
    [InlineData("CALL app.run(", Label = "Unclosed procedure arguments")]
    [InlineData("CALL app.run(1,)", Label = "Trailing argument comma")]
    [InlineData("CALL app.run(value =>)", Label = "Missing named argument value")]
    [InlineData("CALL app.run(value => 1, 2)", Label = "Positional argument after named argument")]
    [InlineData("CALL app.run(*)", Label = "Aggregate wildcard argument")]
    [InlineData("CALL app.run(value => *)", Label = "Named aggregate wildcard")]
    [InlineData("CALL app.run(DISTINCT value)", Label = "Aggregate distinct arguments")]
    [InlineData("CALL app.run(ALL value)", Label = "Aggregate all arguments")]
    [InlineData("CALL app.run(value ORDER BY id)", Label = "Aggregate argument ordering")]
    [InlineData("CALL app.run(value LIMIT 1)", Label = "MySQL aggregate limit")]
    [InlineData("CALL app.run(value SEPARATOR ',')", Label = "MySQL aggregate separator")]
    [InlineData("CALL app.run() FILTER (WHERE TRUE)", Label = "Aggregate filter on procedure")]
    [InlineData("CALL app.run() WITHIN GROUP (ORDER BY id)", Label = "Ordered set on procedure")]
    [InlineData("CALL app.run() OVER ()", Label = "Window on procedure")]
    [InlineData("CALL app.run() + 1", Label = "Expression after procedure invocation")]
    [InlineData("CALL app.run(VARIADIC ARRAY[1], 2)", Label = "Variadic argument must be last")]
    [InlineData("CALL app.run(VARIADIC VARIADIC ARRAY[1])", Label = "Duplicate variadic modifier")]
    [InlineData("CALL app.run(VARIADIC)", Label = "Missing variadic expression")]
    [InlineData("CALL app.run(value => 1, VARIADIC ARRAY[2])", Label = "Unnamed variadic after named argument")]
    [InlineData("CALL 1 + 2", Label = "Non-invocation expression")]
    public void Call_RejectsMalformedOrNonProcedureSyntax(string sql)
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
    [InlineData("ALTER MATERIALIZED VIEW \"App\".\"Summary\" RENAME COLUMN \"Id\" TO \"ItemId\"")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME TO \"next; DROP TABLE items; --\"")]
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
        Assert.Equal("summary_next", Assert.IsType<PgAlterMaterializedViewOperation.RenameTo>(statement.Operation).Name.Name);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("COLUMN ", true)]
    public void AlterMaterializedViews_PreserveColumnRenameFields(string prefix, bool expected)
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement($"ALTER MATERIALIZED VIEW summary RENAME {prefix}id TO item_id"));
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.RenameColumn>(statement.Operation);

        Assert.Equal("id", operation.OldName.Name);
        Assert.Equal("item_id", operation.NewName.Name);
        Assert.Equal(expected, operation.UseColumnKeyword);
    }

    [Fact]
    public void AlterMaterializedViews_PreserveRelocationDestinations()
    {
        var schema = Assert.IsType<AlterMaterializedView>(ParseStatement("ALTER MATERIALIZED VIEW summary SET SCHEMA archive"));
        var tablespace = Assert.IsType<AlterMaterializedView>(ParseStatement("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast"));

        Assert.Equal("archive", Assert.IsType<PgAlterMaterializedViewOperation.SetSchema>(schema.Operation).Name.Name);
        Assert.Equal("fast", Assert.IsType<PgAlterMaterializedViewOperation.SetTablespace>(tablespace.Operation).Name.Name);
    }

    [Theory]
    [InlineData("RENAME TO summary_next")]
    [InlineData("RENAME COLUMN id TO item_id")]
    [InlineData("SET SCHEMA archive")]
    [InlineData("SET TABLESPACE fast")]
    [InlineData("SET (fillfactor = 70, toast.autovacuum_enabled = FALSE)")]
    [InlineData("RESET (fillfactor, toast.autovacuum_enabled)")]
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
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.RenameColumn>(statement.Operation);

        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.All(statement.Name.Values.Append(operation.OldName).Append(operation.NewName), identifier => Assert.Equal(137, identifier.Source!.Value.SourceId));
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is Core.Statements.Components.NonSql.BlockComment { Body: " tail " });
        Assert.Equal("SELECT 2", statements[1].ToSql());
    }

    [Fact]
    public void AlterMaterializedViews_PreserveAtomicBodyBoundariesAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC ALTER MATERIALIZED VIEW app.summary SET SCHEMA archive; SELECT 2; END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        var statement = Assert.Single(body.Statements.OfType<AlterMaterializedView>());

        Assert.IsType<PgAlterMaterializedViewOperation.SetSchema>(statement.Operation);
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
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast, SET TABLESPACE slow")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO owner_name")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER COLUMN id SET STATISTICS 100")]
    [InlineData("ALTER MATERIALIZED VIEW summary DEPENDS ON EXTENSION ext")]
    [InlineData("ALTER MATERIALIZED VIEW ALL IN TABLESPACE fast SET TABLESPACE slow")]
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
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.SetStorageParameters>(statement.Operation);

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
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.ResetStorageParameters>(statement.Operation);

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
        var operation = Assert.IsType<PgAlterMaterializedViewOperation.SetStorageParameters>(statement.Operation);

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
            ? [.. Assert.IsType<PgAlterMaterializedViewOperation.SetStorageParameters>(statement.Operation).Parameters.Select(parameter => parameter.Name)]
            : [.. Assert.IsType<PgAlterMaterializedViewOperation.ResetStorageParameters>(statement.Operation).Parameters];

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
        Assert.IsType<PgAlterMaterializedViewOperation.SetStorageParameters>(statements[0].Operation);
        Assert.IsType<PgAlterMaterializedViewOperation.ResetStorageParameters>(statements[1].Operation);
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
    [InlineData("SET (fillfactor = 70), RESET (fillfactor)")]
    [InlineData("RESET (fillfactor), SET (fillfactor = 70)")]
    public void MaterializedViewStorage_RejectMalformedOrDeferredForms(string clause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"ALTER MATERIALIZED VIEW summary {clause}"));
    }
}
