using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;
using Helpers = TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting.Helpers;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgDropParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("DROP TYPE app.mood")]
    [InlineData("DROP TYPE IF EXISTS app.mood, app.address CASCADE")]
    [InlineData("DROP TYPE \"App\".\"Mood\" RESTRICT")]
    [InlineData("DROP DOMAIN app.label")]
    [InlineData("DROP DOMAIN IF EXISTS app.label, other_label RESTRICT")]
    [InlineData("DROP DOMAIN \"App\".\"Label\" CASCADE")]
    [InlineData("DROP TABLE IF EXISTS app.items, other_items CASCADE")]
    [InlineData("DROP VIEW IF EXISTS app.visible, other_visible RESTRICT")]
    [InlineData("DROP INDEX IF EXISTS app.items_idx, other_idx CASCADE")]
    [InlineData("DROP SCHEMA IF EXISTS app, other_app CASCADE")]
    [InlineData("DROP MATERIALIZED VIEW app.summary RESTRICT")]
    [InlineData("DROP SEQUENCE app.ids CASCADE")]
    [InlineData("DROP TYPE int4, \"char\" CASCADE")]
    [InlineData("DROP TYPE CASCADE")]
    [InlineData("DROP DOMAIN app.label, RESTRICT")]
    [InlineData("DROP TRIGGER audit ON app.items")]
    [InlineData("DROP TRIGGER IF EXISTS \"Audit\" ON \"App\".\"Items\" CASCADE")]
    [InlineData("DROP TRIGGER audit ON items RESTRICT")]
    public void DropSharedTargets_RoundTripSqlAndJson(string sql)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        var formattedStatement = Assert.IsType<DropObject>(ParseStatement(formatted));
        Assert.Equal(statement.ObjectType, formattedStatement.ObjectType);
        Assert.Equal(statement.IfExists, formattedStatement.IfExists);
        Assert.Equal(statement.PgBehavior, formattedStatement.PgBehavior);
        Assert.Equal(statement.OnObject?.Values.Select(identifier => identifier.Name),
            formattedStatement.OnObject?.Values.Select(identifier => identifier.Name));
        Assert.Equal(statement.Names.Count, formattedStatement.Names.Count);
        foreach (var (First, Second) in statement.Names.Zip(formattedStatement.Names))
        {
            Assert.Equal(First.Values.Select(identifier => identifier.Name), Second.Values.Select(identifier => identifier.Name));
        }
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("TYPE")]
    [InlineData("DOMAIN")]
    [InlineData("TABLE")]
    [InlineData("VIEW")]
    [InlineData("INDEX")]
    [InlineData("SCHEMA")]
    public void DropSharedTargets_RetainsFlagsAndExplicitBehavior(string target)
    {
        var omitted = Assert.IsType<DropObject>(ParseStatement($"DROP {target} first_name"));
        var restricted = Assert.IsType<DropObject>(ParseStatement($"DROP {target} IF EXISTS first_name, second_name RESTRICT"));
        var cascading = Assert.IsType<DropObject>(ParseStatement($"DROP {target} first_name, second_name CASCADE"));

        Assert.Equal(DroppableObject.Parse(target), omitted.ObjectType);
        Assert.Null(omitted.PgBehavior);
        Assert.False(omitted.IfExists);
        Assert.Single(omitted.Names);
        Assert.True(restricted.IfExists);
        Assert.Equal(2, restricted.Names.Count);
        Assert.Equal(PgDropBehavior.Restrict, restricted.PgBehavior);
        Assert.Equal(PgDropBehavior.Cascade, cascading.PgBehavior);
        Assert.False(cascading.Temporary);
        Assert.Null(cascading.OnObject);
    }

    [Theory]
    [InlineData("TYPE")]
    [InlineData("DOMAIN")]
    public void DropTypeTargets_ReportUnresolvedReferencesAndRestoreScopes(string target)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement($"DROP {target} app.label, label CASCADE"));
        var tableIdentifier = new ObjectIdentifier("label", new SchemaIdentifier("app", new CatalogIdentifier("def")));
        var table = new PseudoTable("label", tableIdentifier, ["label"], PseudoTableType.Table);
        var tables = new PseudoTableSet(null, [table]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        context.AddLocalVariable(new Identifier("label"));
        using var scope = context.Enter(ReferencedItemsContext.SelectItem);

        var references = statement.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>().ToArray();

        Assert.Equal(["app.label", "label"], references.Select(reference => reference.Name.ToSql()));
        Assert.All(references, reference => Assert.Equal(ItemType.Unknown, reference.Type));
        Assert.All(references, reference => Assert.Null(reference.ObjectHandle));
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Table | ObjectNameFilters.Variable }));
        _ = statement.GetReferencedItems(context).Take(1).ToArray();
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        Assert.Same(tables, context.PseudoTables);
        Assert.Same(table, Assert.Single(tables.GetPseudoTables("label")));
    }

    [Fact]
    public void DropTargets_PreserveSourcesCommentsAndStatementBoundaries()
    {
        const string sql = "/* lead */ DROP TYPE IF EXISTS \"App\".\"Mood\", mood CASCADE /* tail */; DROP DOMAIN app.label RESTRICT; SELECT 1";
        var statements = ParseStatements(sql, sourceId: 167);
        var first = Assert.IsType<DropObject>(statements[0]);

        Assert.Equal(3, statements.Count);
        Assert.Equal(0, first.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, first.Meta.EndPlusTerminatorLength);
        Assert.All(first.Names.SelectMany(name => name.Values), identifier => Assert.Equal(167, identifier.Source!.Value.SourceId));
        Assert.Contains(first.Meta.PreNonSql!, trivia => trivia is NonSql.BlockComment { Body: " lead " });
        Assert.Contains(first.Meta.PostNonSql!, trivia => trivia is NonSql.BlockComment { Body: " tail " });
        Assert.IsType<DropObject>(statements[1]);
        Assert.IsType<Select>(statements[2]);
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void DropTargets_PreserveSqlAtomicBody()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC DROP TYPE IF EXISTS app.mood, app.address CASCADE; DROP DOMAIN app.label RESTRICT; DROP TABLE items CASCADE; END";
        var statement = Assert.IsType<CreateProcedure>(ParseStatement(sql));

        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<CreateProcedure>(ParseStatement(formatted));
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("DROP FUNCTION app.run")]
    [InlineData("DROP FUNCTION app.run()")]
    [InlineData("DROP FUNCTION app.run, other_run(), app.overload(INT)")]
    [InlineData("DROP PROCEDURE app.run()")]
    [InlineData("DROP ROUTINE app.run")]
    [InlineData("DROP ROUTINE app.run(OUT TEXT) CASCADE")]
    [InlineData("DROP FUNCTION app.run(app.label ARRAY[3])")]
    [InlineData("DROP FUNCTION IF EXISTS app.run(INTEGER), app.run(TEXT) CASCADE", "DROP FUNCTION IF EXISTS app.run(INT), app.run(TEXT) CASCADE")]
    [InlineData("DROP PROCEDURE app.run(IN id INTEGER, OUT result TEXT) RESTRICT", "DROP PROCEDURE app.run(IN id INT, OUT result TEXT) RESTRICT")]
    [InlineData("DROP PROCEDURE app.run(INOUT app.label, VARIADIC TEXT[])")]
    [InlineData("DROP ROUTINE IF EXISTS \"App\".\"Run\"(arg \"App\".\"Label\"[]), other_run RESTRICT")]
    [InlineData("DROP ROUTINE app.run(DOUBLE PRECISION, TIMESTAMP WITH TIME ZONE)")]
    public void DropRoutine_RoundTripsSqlJsonAndFormatting(string sql, string? expectedSql = null)
    {
        var statement = Assert.IsType<DropRoutine>(ParseStatement(sql));

        Assert.Equal(expectedSql ?? sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<DropRoutine>(ParseStatement(formatted));
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("FUNCTION")]
    [InlineData("PROCEDURE")]
    [InlineData("ROUTINE")]
    public void DropRoutine_PreservesOmittedEmptyAndTypedSignatures(string target)
    {
        var omitted = Assert.IsType<DropRoutine>(ParseStatement($"DROP {target} app.run"));
        var empty = Assert.IsType<DropRoutine>(ParseStatement($"DROP {target} app.run()"));
        var typed = Assert.IsType<DropRoutine>(ParseStatement($"DROP {target} IF EXISTS app.run(IN id INT, OUT result app.label, INOUT TEXT, VARIADIC TEXT[]), other_run RESTRICT"));

        Assert.Equal(DroppableObject.Parse(target), typed.ObjectType);
        Assert.Null(Assert.Single(omitted.Routines).Parameters);
        Assert.Empty(Assert.Single(empty.Routines).Parameters!);
        Assert.False(omitted.IfExists);
        Assert.Null(omitted.PgBehavior);
        Assert.True(typed.IfExists);
        Assert.Equal(PgDropBehavior.Restrict, typed.PgBehavior);
        Assert.Equal(2, typed.Routines.Count);
        Assert.Null(typed.Routines[1].Parameters);
        SqlValueList<StatementRoutineParameter> parameters = typed.Routines[0].Parameters!;
        Assert.Equal(4, parameters.Count);
        Assert.Equal("id", parameters[0].Name!.Name);
        Assert.Equal("IN", parameters[0].Direction!.ToString());
        Assert.IsType<DataTypeReference.BuiltIn>(parameters[0].DataType);
        Assert.Equal("result", parameters[1].Name!.Name);
        Assert.Equal("OUT", parameters[1].Direction!.ToString());
        Assert.Equal("app.label", Assert.IsType<DataTypeReference.Named>(parameters[1].DataType).Name.ToSql());
        Assert.Null(parameters[2].Name);
        Assert.Equal("INOUT", parameters[2].Direction!.ToString());
        Assert.Equal("VARIADIC", parameters[3].Direction!.ToString());
        Assert.IsType<DataTypeReference.Array>(parameters[3].DataType);
        Assert.All(parameters, parameter => Assert.Null(parameter.ParameterDefault));
    }

    [Theory]
    [InlineData("FUNCTION", true)]
    [InlineData("PROCEDURE", true)]
    [InlineData("ROUTINE", true)]
    [InlineData("FUNCTION", false)]
    [InlineData("PROCEDURE", false)]
    [InlineData("ROUTINE", false)]
    public void DropRoutine_ReportsTargetsAndNamedTypesWithoutTableOrVariableConfusion(string target, bool signature)
    {
        var statement = Assert.IsType<DropRoutine>(ParseStatement($"DROP {target} app.run(IN argument app.label[], OUT result TEXT), app.run{(signature ? "(INT)" : "")} CASCADE"));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var tables = new PseudoTableSet(schema,
        [
            new PseudoTable("run", new ObjectIdentifier("run", schema), ["argument"], PseudoTableType.Table),
            new PseudoTable("label", new ObjectIdentifier("label", schema), ["result"], PseudoTableType.Table)
        ]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        context.AddLocalVariable(new Identifier("argument"));
        using var scope = context.Enter(ReferencedItemsContext.SelectItem);

        var references = statement.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>().ToArray();

        Assert.Equal(["app.run", "app.label", "app.run"], references.Select(reference => reference.Name.ToSql()));
        Assert.All(references, reference => Assert.Equal(ItemType.Unknown, reference.Type));
        Assert.All(references, reference => Assert.Null(reference.ObjectHandle));
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Table | ObjectNameFilters.Variable, PseudoTables = tables }));
        _ = statement.GetReferencedItems(context).Take(1).ToArray();
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        _ = statement.GetReferencedItems(context).Take(2).ToArray();
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        Assert.Same(tables, context.PseudoTables);
    }

    [Fact]
    public void DropTrigger_SeparatesTriggerAndTableReferences()
    {
        var statement = Assert.IsType<DropObject>(ParseStatement("DROP TRIGGER IF EXISTS audit ON app.items CASCADE"));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var tables = new PseudoTableSet(schema,
        [
            new PseudoTable("audit", new ObjectIdentifier("audit", schema), [], PseudoTableType.Table),
            new PseudoTable("items", new ObjectIdentifier("items", schema), [], PseudoTableType.Table)
        ]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        using var scope = context.Enter(ReferencedItemsContext.WhereClause);

        var references = statement.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>().ToArray();

        Assert.Equal(DroppableObject.Trigger, statement.ObjectType);
        Assert.True(statement.IfExists);
        Assert.Equal(PgDropBehavior.Cascade, statement.PgBehavior);
        Assert.Equal("audit", Assert.Single(statement.Names).ToSql());
        Assert.Equal("app.items", statement.OnObject!.ToSql());
        Assert.Equal(["audit", "app.items"], references.Select(reference => reference.Name.ToSql()));
        Assert.Equal(ItemType.Unknown, references[0].Type);
        Assert.Null(references[0].ObjectHandle);
        Assert.Equal(ItemType.Table, references[1].Type);
        Assert.NotNull(references[1].ObjectHandle);
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        _ = statement.GetReferencedItems(context).Take(1).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        var tableOnly = Assert.IsType<ItemRef.ObjectNameRef>(Assert.Single(statement.GetReferencedItems(
            new ReferencedItemsManager { Filters = ObjectNameFilters.Table, PseudoTables = tables })));
        Assert.Equal("app.items", tableOnly.Name.ToSql());
        Assert.Same(tables, context.PseudoTables);
    }

    [Fact]
    public void DropRoutineAndTrigger_PreserveSourcesCommentsAndStatementBoundaries()
    {
        const string sql = "/* lead */ DROP FUNCTION IF EXISTS \"App\".\"Run\"(IN \"Arg\" \"App\".\"Label\"[]) CASCADE /* tail */; DROP TRIGGER \"Audit\" ON \"App\".\"Items\" RESTRICT; SELECT 1";
        var statements = ParseStatements(sql, sourceId: 173);
        var routine = Assert.IsType<DropRoutine>(statements[0]);
        var trigger = Assert.IsType<DropObject>(statements[1]);
        PgRoutineReference target = Assert.Single(routine.Routines);
        StatementRoutineParameter parameter = Assert.Single(target.Parameters!);
        var namedType = Assert.IsType<DataTypeReference.Named>(Assert.IsType<DataTypeReference.Array>(parameter.DataType).ElementType);

        Assert.Equal(3, statements.Count);
        Assert.Equal(0, routine.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, routine.Meta.EndPlusTerminatorLength);
        Assert.All(target.Name.Values, identifier => Assert.Equal(173, identifier.Source!.Value.SourceId));
        Assert.Equal(173, parameter.Name!.Source!.Value.SourceId);
        Assert.All(namedType.Name.Values, identifier => Assert.Equal(173, identifier.Source!.Value.SourceId));
        Assert.All(trigger.OnObject!.Values, identifier => Assert.Equal(173, identifier.Source!.Value.SourceId));
        Assert.Equal(173, Assert.Single(Assert.Single(trigger.Names).Values).Source!.Value.SourceId);
        Assert.Contains(routine.Meta.PreNonSql!, trivia => trivia is NonSql.BlockComment { Body: " lead " });
        Assert.Contains(routine.Meta.PostNonSql!, trivia => trivia is NonSql.BlockComment { Body: " tail " });
        Assert.IsType<Select>(statements[2]);
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void DropRoutineAndTrigger_PreserveSqlAtomicBody()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC DROP FUNCTION IF EXISTS app.other_run(INT), another_run() CASCADE; DROP ROUTINE app.last_run RESTRICT; DROP TRIGGER audit ON app.items CASCADE; END";
        var statement = Assert.IsType<CreateProcedure>(ParseStatement(sql));

        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<CreateProcedure>(ParseStatement(formatted));
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("DROP FUNCTION app.run(INTEGER DEFAULT 1)")]
    [InlineData("DROP PROCEDURE app.run(IN id INTEGER = 1)")]
    [InlineData("DROP ROUTINE app.run(,)")]
    [InlineData("DROP ROUTINE app.run(INTEGER,)")]
    [InlineData("DROP FUNCTION app.run() CASCADE RESTRICT")]
    [InlineData("DROP FUNCTION app.run(),")]
    [InlineData("DROP ROUTINE")]
    [InlineData("DROP FUNCTION IF EXISTS")]
    [InlineData("DROP FUNCTION app.run(INTEGER")]
    [InlineData("DROP PROCEDURE app.run(OUT)")]
    [InlineData("DROP ROUTINE app.run(INT) ON app.items")]
    [InlineData("DROP FUNCTION app.run(INT) LANGUAGE SQL")]
    [InlineData("DROP FUNCTION app.run(INT) RETURNS TEXT")]
    [InlineData("DROP PROCEDURE app.run(@id INT)")]
    [InlineData("DROP FUNCTION app.run(id INT READONLY)")]
    [InlineData("DROP TEMPORARY FUNCTION app.run()")]
    public void DropRoutine_RejectsMalformedSignatures(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("DROP TYPE")]
    [InlineData("DROP DOMAIN IF EXISTS")]
    [InlineData("DROP TYPE app.mood,")]
    [InlineData("DROP DOMAIN app.label CASCADE RESTRICT")]
    [InlineData("DROP TABLE items CASCADE, other_items")]
    [InlineData("DROP TYPE app.mood ON items")]
    [InlineData("DROP TYPE app.mood(10)")]
    [InlineData("DROP TYPE app.mood[]")]
    [InlineData("DROP TYPE app.mood CASCADE CASCADE")]
    [InlineData("DROP INDEX items_idx ON items")]
    [InlineData("DROP TEMPORARY TYPE app.mood")]
    [InlineData("DROP TEMPORARY TABLE items")]
    [InlineData("DROP TEMPORARY VIEW visible")]
    [InlineData("DROP DATABASE app CASCADE")]
    [InlineData("DROP TRIGGER audit")]
    [InlineData("DROP TRIGGER audit CASCADE")]
    [InlineData("DROP TRIGGER audit ON")]
    [InlineData("DROP TRIGGER app.audit ON items")]
    [InlineData("DROP TRIGGER audit, other_audit ON items")]
    [InlineData("DROP TRIGGER audit ON items CASCADE RESTRICT")]
    [InlineData("DROP TRIGGER audit ON items, other_items")]
    public void DropSharedTargets_RejectMalformedAndUnsupportedClauses(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
