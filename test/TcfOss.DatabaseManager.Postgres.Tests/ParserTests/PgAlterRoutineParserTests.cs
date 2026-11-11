using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

using Helpers = TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting.Helpers;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgAlterRoutineParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("ALTER FUNCTION app.calculate IMMUTABLE")]
    [InlineData("ALTER FUNCTION app.calculate() STRICT STABLE PARALLEL SAFE COST 10 ROWS 20 RESTRICT")]
    [InlineData("ALTER FUNCTION app.calculate(INT) CALLED ON NULL INPUT VOLATILE")]
    [InlineData("ALTER FUNCTION app.calculate(IN arg app.amount[], OUT result app.result) RETURNS NULL ON NULL INPUT")]
    [InlineData("ALTER FUNCTION app.calculate() SECURITY DEFINER")]
    [InlineData("ALTER FUNCTION app.calculate() EXTERNAL SECURITY INVOKER")]
    [InlineData("ALTER PROCEDURE app.run(INOUT arg app.amount) SECURITY DEFINER")]
    [InlineData("ALTER PROCEDURE app.run() SET search_path = 'app', 'public' RESET work_mem RESTRICT")]
    [InlineData("ALTER FUNCTION app.calculate() SET work_mem TO DEFAULT RESET ALL")]
    [InlineData("ALTER FUNCTION app.calculate() SET work_mem = DEFAULT SET search_path FROM CURRENT")]
    [InlineData("ALTER FUNCTION app.calculate() RENAME TO new_calculate")]
    [InlineData("ALTER PROCEDURE app.run() OWNER TO CURRENT_ROLE")]
    [InlineData("ALTER FUNCTION app.calculate() SET SCHEMA archive")]
    [InlineData("ALTER PROCEDURE app.run() DEPENDS ON EXTENSION app_extension")]
    [InlineData("ALTER FUNCTION app.calculate() NO DEPENDS ON EXTENSION app_extension")]
    [InlineData("ALTER FUNCTION app.calculate() PARALLEL UNSAFE")]
    [InlineData("ALTER FUNCTION app.calculate() PARALLEL RESTRICTED")]
    [InlineData("ALTER FUNCTION app.calculate() COST 1.5 ROWS 1e2")]
    [InlineData("ALTER FUNCTION app.calculate(VARIADIC args app.amount[]) IMMUTABLE")]
    [InlineData("ALTER PROCEDURE app.run SECURITY INVOKER RESET ALL")]
    [InlineData("ALTER PROCEDURE app.run() EXTERNAL SECURITY DEFINER RESTRICT")]
    [InlineData("ALTER PROCEDURE app.run() SET work_mem TO DEFAULT")]
    [InlineData("ALTER PROCEDURE app.run() SET work_mem TO 'DEFAULT'")]
    [InlineData("ALTER PROCEDURE app.run() SET work_mem FROM CURRENT")]
    [InlineData("ALTER FUNCTION app.calculate() RESET \"ALL\"")]
    [InlineData("ALTER FUNCTION app.calculate() SET app.option = 3 RESET app.option")]
    [InlineData("ALTER FUNCTION app.calculate() SET work_mem = '4MB' RESET work_mem SET work_mem TO DEFAULT")]
    [InlineData("ALTER FUNCTION app.calculate() OWNER TO CURRENT_USER")]
    [InlineData("ALTER FUNCTION app.calculate() OWNER TO SESSION_USER")]
    [InlineData("ALTER FUNCTION app.calculate() OWNER TO owner_role")]
    [InlineData("ALTER FUNCTION app.calculate() OWNER TO \"Owner; SELECT 1\"")]
    [InlineData("ALTER PROCEDURE app.run() RENAME TO \"Run; SELECT 1\"")]
    [InlineData("ALTER PROCEDURE app.run() SET SCHEMA \"Archive\"")]
    [InlineData("ALTER PROCEDURE app.run() NO DEPENDS ON EXTENSION \"Extension\"")]
    public void AlterRoutine_RoundTripsSqlJsonAndFormatting(string sql)
    {
        var statement = Assert.IsType<AlterRoutine>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<AlterRoutine>(ParseStatement(formatted));
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void AlterRoutine_DistinguishesSignaturesAndConfigurationForms()
    {
        var omitted = Assert.IsType<AlterRoutine>(ParseStatement("ALTER FUNCTION app.calculate IMMUTABLE"));
        var empty = Assert.IsType<AlterRoutine>(ParseStatement("ALTER FUNCTION app.calculate() IMMUTABLE"));
        const string sql = """
            ALTER PROCEDURE app.run()
                SET work_mem = DEFAULT
                SET work_mem TO 'DEFAULT'
                RESET ALL
                RESET "ALL"
                SET search_path FROM CURRENT RESTRICT
            """;
        var statement = Assert.IsType<AlterRoutine>(ParseStatement(sql));

        Assert.Null(omitted.Routine.Parameters);
        Assert.Empty(empty.Routine.Parameters!);
        Assert.Equal(PgRoutineKind.Function, omitted.Kind);
        Assert.Equal(PgRoutineKind.Procedure, statement.Kind);
        Assert.True(statement.Restrict);
        Assert.False(empty.Restrict);

        var setting0 = Assert.IsType<PgRoutineSetting.ToDefault>(
            Assert.IsType<PgStatementRoutineOption.RoutineSetting>(
                Assert.IsType<PgAlterRoutineOperation.SetOption>(statement.Operations[0]).Value).Setting);
        Assert.Equal(SettingAssignmentOperator.EqualTo, setting0.Op);

        var setting1 = Assert.IsType<PgRoutineSetting.ToValues>(
            Assert.IsType<PgStatementRoutineOption.RoutineSetting>(
                Assert.IsType<PgAlterRoutineOperation.SetOption>(statement.Operations[1]).Value).Setting);
        Assert.Equal(SettingAssignmentOperator.To, setting1.Op);
        Assert.Equal("DEFAULT", Assert.IsType<Value.SingleQuotedString>(Assert.Single(setting1.Values)).Value);

        Assert.IsType<PgAlterRoutineOperation.ResetAll>(statement.Operations[2]);

        var reset = Assert.IsType<PgAlterRoutineOperation.Reset>(statement.Operations[3]);
        Assert.Equal("\"ALL\"", reset.Name.ToSql());

        Assert.IsType<PgRoutineSetting.FromCurrent>(Assert.IsType<PgStatementRoutineOption.RoutineSetting>(
            Assert.IsType<PgAlterRoutineOperation.SetOption>(statement.Operations[4]).Value).Setting);
    }

    [Fact]
    public void AlterRoutine_PreservesSourcesCommentsAndFollowingStatements()
    {
        const string sql = "/* lead */ ALTER FUNCTION \"App\".\"Calculate\"(IN \"Arg\" \"App\".\"Amount\"[]) COST 10 RESET \"App\".\"Setting\" RESTRICT /* tail */; ALTER PROCEDURE app.run DEPENDS ON EXTENSION \"Ext\"; SELECT 1";
        var statements = ParseStatements(sql, sourceId: 187);
        var statement = Assert.IsType<AlterRoutine>(statements[0]);
        StatementRoutineParameter parameter = Assert.Single(statement.Routine.Parameters!);
        var type = Assert.IsType<DataTypeReference.Named>(Assert.IsType<DataTypeReference.Array>(parameter.DataType).ElementType);
        var reset = Assert.IsType<PgAlterRoutineOperation.Reset>(statement.Operations[1]);
        var second = Assert.IsType<AlterRoutine>(statements[1]);
        var extension = Assert.IsType<PgAlterRelationOperation.DependsOnExtension>(
            Assert.IsType<PgAlterRoutineOperation.Relation>(Assert.Single(second.Operations)).Value);

        Assert.Equal(3, statements.Count);
        Assert.All(statement.Routine.Name.Values, identifier => Assert.Equal(187, identifier.Source!.Value.SourceId));
        Assert.All(type.Name.Values, identifier => Assert.Equal(187, identifier.Source!.Value.SourceId));
        Assert.All(reset.Name.Values, identifier => Assert.Equal(187, identifier.Source!.Value.SourceId));
        Assert.Equal(187, extension.Name.Source!.Value.SourceId);
        Assert.False(extension.No);
        Assert.IsType<Select>(statements[2]);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is NonSql.BlockComment { Body: " tail " });
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void AlterRoutine_ReportsWrittenTargetAndTypesAndRestoresScopes()
    {
        const string sql = "ALTER FUNCTION app.calculate(IN arg app.amount[], OUT result app.result) SET app.option TO DEFAULT RESET app.other";
        var statement = Assert.IsType<AlterRoutine>(ParseStatement(sql));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var tables = new PseudoTableSet(schema,
        [
            new PseudoTable("calculate", new ObjectIdentifier("calculate", schema), [], PseudoTableType.Table),
            new PseudoTable("amount", new ObjectIdentifier("amount", schema), [], PseudoTableType.Table),
            new PseudoTable("result", new ObjectIdentifier("result", schema), [], PseudoTableType.Table)
        ]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        context.AddLocalVariable(new Identifier("calculate"));
        context.AddLocalVariable(new Identifier("amount"));
        using var scope = context.Enter(ReferencedItemsContext.WhereClause);

        ItemRef[] references = [.. statement.GetReferencedItems(context)];

        Assert.Equal(["app.calculate", "app.amount", "app.result"], references.OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql()));
        Assert.All(references, reference => Assert.Equal(ItemType.Unknown, reference.Type));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Table, PseudoTables = tables }));
        _ = statement.GetReferencedItems(context).Take(1).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        _ = statement.GetReferencedItems(context).Take(2).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        Assert.Same(tables, context.PseudoTables);
        string[] actions = ["OWNER TO owner_role", "SET SCHEMA archive", "RENAME TO new_calculate", "DEPENDS ON EXTENSION extension_name"];
        foreach (string action in actions)
        {
            var management = Assert.IsType<AlterRoutine>(ParseStatement($"ALTER FUNCTION app.calculate {action}"));
            Assert.Single(management.GetReferencedItems(context));
            Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        }
    }

    [Fact]
    public void AlterRoutine_RoundTripsAtomicBodyAndOuterBoundary()
    {
        const string sql = "CREATE PROCEDURE app.manage() LANGUAGE SQL BEGIN ATOMIC ALTER FUNCTION app.calculate(app.amount[]) STABLE SET work_mem TO DEFAULT RESET ALL RESTRICT; ALTER PROCEDURE app.run() OWNER TO CURRENT_ROLE; END; SELECT 1";
        var statements = ParseStatements(sql);
        var statement = Assert.IsType<CreateProcedure>(statements[0]);

        Assert.Equal(2, statements.Count);
        Assert.IsType<Select>(statements[1]);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] roundTrip = [statement];
        Assert.Equal(roundTrip, Serialization.FromJson<Statement[]>(Serialization.ToJson(roundTrip)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("ALTER FUNCTION app.calculate()")]
    [InlineData("ALTER FUNCTION IF EXISTS app.calculate() IMMUTABLE")]
    [InlineData("ALTER FUNCTION app.calculate(INT DEFAULT 1) IMMUTABLE")]
    [InlineData("ALTER FUNCTION app.calculate(arg INT = 1) IMMUTABLE")]
    [InlineData("ALTER FUNCTION app.calculate( IMMUTABLE")]
    [InlineData("ALTER FUNCTION app.calculate() IMMUTABLE, STRICT")]
    [InlineData("ALTER FUNCTION app.calculate() LANGUAGE SQL")]
    [InlineData("ALTER FUNCTION app.calculate() RETURNS TEXT")]
    [InlineData("ALTER FUNCTION app.calculate() AS 'body'")]
    [InlineData("ALTER FUNCTION app.calculate() LEAKPROOF")]
    [InlineData("ALTER FUNCTION app.calculate() SUPPORT app.support")]
    [InlineData("ALTER FUNCTION app.calculate() COST 0")]
    [InlineData("ALTER FUNCTION app.calculate() ROWS -1")]
    [InlineData("ALTER FUNCTION app.calculate() PARALLEL INVALID")]
    [InlineData("ALTER FUNCTION app.calculate() SECURITY owner_role")]
    [InlineData("ALTER FUNCTION app.calculate() SET work_mem")]
    [InlineData("ALTER FUNCTION app.calculate() SET work_mem TO")]
    [InlineData("ALTER FUNCTION app.calculate() SET work_mem FROM other")]
    [InlineData("ALTER FUNCTION app.calculate() RESET")]
    [InlineData("ALTER FUNCTION app.calculate() RESET work_mem, search_path")]
    [InlineData("ALTER FUNCTION app.calculate() RENAME TO app.new_name")]
    [InlineData("ALTER FUNCTION app.calculate() RENAME TO new_name STRICT")]
    [InlineData("ALTER FUNCTION app.calculate() OWNER TO 'owner_role'")]
    [InlineData("ALTER FUNCTION app.calculate() OWNER TO owner_role@host")]
    [InlineData("ALTER FUNCTION app.calculate() SET SCHEMA app.archive")]
    [InlineData("ALTER FUNCTION app.calculate() DEPENDS EXTENSION ext")]
    [InlineData("ALTER FUNCTION app.calculate() DEPENDS ON EXTENSION app.ext")]
    [InlineData("ALTER FUNCTION app.calculate() NO DEPENDS ON")]
    [InlineData("ALTER FUNCTION app.calculate() STRICT OWNER TO owner_role")]
    [InlineData("ALTER FUNCTION app.calculate() RENAME TO new_name RESTRICT")]
    [InlineData("ALTER FUNCTION app.calculate() STRICT CASCADE")]
    [InlineData("ALTER PROCEDURE app.run() IMMUTABLE")]
    [InlineData("ALTER PROCEDURE app.run() STRICT")]
    [InlineData("ALTER PROCEDURE app.run() CALLED ON NULL INPUT")]
    [InlineData("ALTER PROCEDURE app.run() RETURNS NULL ON NULL INPUT")]
    [InlineData("ALTER PROCEDURE app.run() PARALLEL SAFE")]
    [InlineData("ALTER PROCEDURE app.run() COST 10")]
    [InlineData("ALTER PROCEDURE app.run() ROWS 10")]
    [InlineData("ALTER ROUTINE app.run() SECURITY DEFINER")]
    public void AlterRoutine_RejectsMalformedInapplicableAndDeferredForms(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
