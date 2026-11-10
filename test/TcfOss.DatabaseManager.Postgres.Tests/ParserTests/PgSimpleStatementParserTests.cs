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
    [InlineData("ALTER COLUMN id SET STATISTICS -1", typeof(PgMaterializedViewColumnOperation.SetStatistics), true)]
    [InlineData("ALTER id SET STATISTICS DEFAULT", typeof(PgMaterializedViewColumnOperation.SetDefaultStatistics), false)]
    [InlineData("ALTER COLUMN id SET (n_distinct = -0.5, n_distinct_inherited = 100)", typeof(PgMaterializedViewColumnOperation.SetOptions), true)]
    [InlineData("ALTER id RESET (n_distinct, n_distinct_inherited)", typeof(PgMaterializedViewColumnOperation.ResetOptions), false)]
    [InlineData("ALTER id SET STORAGE PLAIN", typeof(PgMaterializedViewColumnOperation.SetStorage), false)]
    [InlineData("ALTER id SET STORAGE EXTERNAL", typeof(PgMaterializedViewColumnOperation.SetStorage), false)]
    [InlineData("ALTER id SET STORAGE EXTENDED", typeof(PgMaterializedViewColumnOperation.SetStorage), false)]
    [InlineData("ALTER id SET STORAGE MAIN", typeof(PgMaterializedViewColumnOperation.SetStorage), false)]
    [InlineData("ALTER id SET STORAGE DEFAULT", typeof(PgMaterializedViewColumnOperation.SetStorage), false)]
    [InlineData("ALTER id SET COMPRESSION lz4", typeof(PgMaterializedViewColumnOperation.SetCompression), false)]
    [InlineData("ALTER id SET COMPRESSION \"default\"", typeof(PgMaterializedViewColumnOperation.SetCompression), false)]
    [InlineData("ALTER id SET COMPRESSION DEFAULT", typeof(PgMaterializedViewColumnOperation.SetDefaultCompression), false)]
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
        Assert.Equal(100, Assert.IsType<PgMaterializedViewColumnOperation.SetStatistics>(statistics.Operation).Target);
        Assert.Equal("id", statistics.Name.Name);
        var storage = Assert.IsType<PgAlterMaterializedViewOperation.AlterColumn>(statement.Operations[1]);
        Assert.Equal(PgColumnStorageMode.Extended, Assert.IsType<PgMaterializedViewColumnOperation.SetStorage>(storage.Operation).Mode);
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
