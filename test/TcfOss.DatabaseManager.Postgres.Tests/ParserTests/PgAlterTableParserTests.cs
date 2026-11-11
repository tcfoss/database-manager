using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

using Helpers = TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting.Helpers;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgAlterTableParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("ALTER TABLE items ADD COLUMN IF NOT EXISTS label TEXT")]
    [InlineData("ALTER TABLE IF EXISTS ONLY app.items ADD IF NOT EXISTS label app.label[]")]
    [InlineData("ALTER TABLE items DROP COLUMN IF EXISTS label CASCADE")]
    [InlineData("ALTER TABLE items DROP IF EXISTS label RESTRICT")]
    [InlineData("ALTER TABLE items DROP label")]
    [InlineData("ALTER TABLE items RENAME COLUMN label TO new_label")]
    [InlineData("ALTER TABLE items RENAME label TO new_label")]
    [InlineData("ALTER TABLE items ADD COLUMN label TEXT, DROP COLUMN old_label RESTRICT")]
    [InlineData("ALTER TABLE items ALTER COLUMN label TYPE TEXT")]
    [InlineData("ALTER TABLE items ALTER label SET DATA TYPE app.label[]")]
    [InlineData("ALTER TABLE items ALTER label TYPE TEXT COLLATE \"C\" USING label")]
    [InlineData("ALTER TABLE items ALTER COLUMN label SET NOT NULL")]
    [InlineData("ALTER TABLE items ALTER label DROP NOT NULL")]
    [InlineData("ALTER TABLE items ALTER label SET DEFAULT 5")]
    [InlineData("ALTER TABLE items ALTER label SET DEFAULT 1 + 2", "ALTER TABLE items ALTER label SET DEFAULT (1 + 2)")]
    [InlineData("ALTER TABLE items ALTER COLUMN label SET DEFAULT (app.make_label())")]
    [InlineData("ALTER TABLE items ALTER label DROP DEFAULT")]
    [InlineData("ALTER TABLE items ALTER COLUMN label SET EXPRESSION AS (other_label)")]
    [InlineData("ALTER TABLE items ALTER label DROP EXPRESSION")]
    [InlineData("ALTER TABLE items ALTER COLUMN label DROP EXPRESSION IF EXISTS")]
    [InlineData("ALTER TABLE items ALTER label SET STATISTICS 100")]
    [InlineData("ALTER TABLE items ALTER COLUMN label SET STATISTICS DEFAULT")]
    [InlineData("ALTER TABLE items ALTER label SET STATISTICS -1")]
    [InlineData("ALTER TABLE items ALTER label SET (n_distinct = -0.5, n_distinct_inherited = 5)")]
    [InlineData("ALTER TABLE items ALTER COLUMN label RESET (n_distinct, n_distinct_inherited)")]
    [InlineData("ALTER TABLE items ALTER label SET STORAGE EXTENDED")]
    [InlineData("ALTER TABLE items ALTER label SET STORAGE PLAIN")]
    [InlineData("ALTER TABLE items ALTER label SET STORAGE MAIN")]
    [InlineData("ALTER TABLE items ALTER label SET STORAGE EXTERNAL")]
    [InlineData("ALTER TABLE items ALTER COLUMN label SET STORAGE DEFAULT")]
    [InlineData("ALTER TABLE items ALTER label SET COMPRESSION lz4")]
    [InlineData("ALTER TABLE items ALTER label SET COMPRESSION \"default\"")]
    [InlineData("ALTER TABLE items ALTER COLUMN label SET COMPRESSION DEFAULT")]
    [InlineData("ALTER TABLE items ALTER label TYPE TEXT, ALTER label SET NOT NULL, ALTER label SET STATISTICS 10")]
    [InlineData("ALTER TABLE items ALTER id SET GENERATED ALWAYS RESTART 20, ALTER label TYPE TEXT, ALTER label SET DEFAULT 5, ALTER label SET STATISTICS 100")]
    [InlineData("ALTER TABLE items ADD COLUMN label TEXT DEFAULT 'new' NOT NULL")]
    [InlineData("ALTER TABLE items ADD COLUMN total INT DEFAULT 1 + 2 NOT NULL", "ALTER TABLE items ADD COLUMN total INT DEFAULT (1 + 2) NOT NULL")]
    [InlineData("ALTER TABLE items ADD COLUMN label TEXT DEFAULT 'a' || 'b' COLLATE \"C\"", "ALTER TABLE items ADD COLUMN label TEXT DEFAULT ('a' || 'b') COLLATE \"C\"")]
    [InlineData("ALTER TABLE items ALTER label SET DEFAULT 'a' COLLATE \"C\"", "ALTER TABLE items ALTER label SET DEFAULT ('a' COLLATE \"C\")")]
    [InlineData("ALTER TABLE items ADD COLUMN id INT GENERATED ALWAYS AS IDENTITY")]
    [InlineData("ALTER TABLE items OWNER TO CURRENT_ROLE")]
    [InlineData("ALTER TABLE items OWNER TO CURRENT_USER")]
    [InlineData("ALTER TABLE items OWNER TO SESSION_USER")]
    [InlineData("ALTER TABLE items OWNER TO \"Role\"")]
    [InlineData("ALTER TABLE IF EXISTS items SET SCHEMA \"Archive\"")]
    [InlineData("ALTER TABLE items SET SCHEMA archive")]
    [InlineData("ALTER TABLE items SET TABLESPACE fast")]
    [InlineData("ALTER TABLE items SET ACCESS METHOD heap")]
    [InlineData("ALTER TABLE items SET ACCESS METHOD DEFAULT")]
    [InlineData("ALTER TABLE items SET ACCESS METHOD \"default\"")]
    [InlineData("ALTER TABLE items CLUSTER ON items_idx")]
    [InlineData("ALTER TABLE items SET WITHOUT CLUSTER")]
    [InlineData("ALTER TABLE items SET (fillfactor = 70, toast.autovacuum_enabled = FALSE)")]
    [InlineData("ALTER TABLE items RESET (fillfactor, toast.autovacuum_enabled)")]
    [InlineData("ALTER TABLE items SET (autovacuum_enabled, toast.autovacuum_enabled = off)", "ALTER TABLE items SET (autovacuum_enabled, toast.autovacuum_enabled = 'off')")]
    [InlineData("ALTER TABLE items SET LOGGED")]
    [InlineData("ALTER TABLE items SET UNLOGGED")]
    [InlineData("ALTER TABLE items INHERIT app.parent")]
    [InlineData("ALTER TABLE items NO INHERIT app.parent")]
    [InlineData("ALTER TABLE items OF app.item_type")]
    [InlineData("ALTER TABLE items NOT OF")]
    [InlineData("ALTER TABLE items OWNER TO CURRENT_USER, SET TABLESPACE fast, SET LOGGED, ALTER label SET STATISTICS 100")]
    public void AlterTableColumns_RoundTripSqlJsonAndFormatting(string sql, string? expectedSql = null)
    {
        var statement = Assert.IsType<AlterTable>(ParseStatement(sql));

        Assert.Equal(expectedSql ?? sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<AlterTable>(ParseStatement(formatted));
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void AlterTableColumns_RetainsStructuralFlagsTypeAndExpressionDetails()
    {
        const string sql = "ALTER TABLE IF EXISTS ONLY app.items ADD COLUMN IF NOT EXISTS label app.label[], ALTER label SET DATA TYPE app.new_label[] COLLATE \"App\".\"Collation\" USING label, ALTER COLUMN label SET NOT NULL, DROP IF EXISTS old_label CASCADE";
        var statement = Assert.IsType<AlterTable>(ParseStatement(sql));
        var add = Assert.IsType<AlterTableOperation.AddColumn>(statement.Operations[0]);
        var alter = Assert.IsType<AlterTableOperation.PgColumn>(statement.Operations[1]);
        var change = Assert.IsType<PgAlterColumnOperation.ChangeType>(alter.Operation);
        var nullability = Assert.IsType<AlterTableOperation.PgColumn>(statement.Operations[2]);
        var drop = Assert.IsType<AlterTableOperation.DropColumn>(statement.Operations[3]);

        Assert.True(statement.IfExists);
        Assert.True(statement.Only);
        Assert.True(add.PrintColumn);
        Assert.True(add.IfNotExists);
        Assert.False(alter.PrintColumn);
        Assert.True(change.UseSetData);
        Assert.Equal("app.new_label", Assert.IsType<DataTypeReference.Named>(Assert.IsType<DataTypeReference.Array>(change.DataType).ElementType).Name.ToSql());
        Assert.Equal("\"App\".\"Collation\"", change.Collation!.ToSql());
        Assert.Equal("label", Assert.IsType<SingleIdentifier>(change.Using).Identifier.Name);
        Assert.True(nullability.PrintColumn);
        Assert.True(Assert.IsType<PgAlterColumnOperation.Nullability>(nullability.Operation).NotNull);
        Assert.False(drop.PrintColumn);
        Assert.True(drop.IfExists);
        Assert.Equal(PgDropBehavior.Cascade, drop.PgBehavior);
        var plainDrop = Assert.IsType<AlterTableOperation.DropColumn>(Assert.Single(
            Assert.IsType<AlterTable>(ParseStatement("ALTER TABLE items DROP COLUMN label")).Operations));
        Assert.Null(plainDrop.PgBehavior);
        Assert.False(plainDrop.IfExists);
    }

    [Fact]
    public void AlterTableColumns_DistinguishesDefaultConfigurationAndQuotedCompression()
    {
        const string sql = "ALTER TABLE items ALTER label SET STATISTICS -1, ALTER label SET STATISTICS DEFAULT, ALTER label SET COMPRESSION \"default\", ALTER label SET COMPRESSION DEFAULT";
        var statement = Assert.IsType<AlterTable>(ParseStatement(sql));
        var configurations = statement.Operations.Cast<AlterTableOperation.PgColumn>()
            .Select(operation => Assert.IsType<PgAlterColumnOperation.Configuration>(operation.Operation).Operation).ToArray();

        Assert.Equal(-1, Assert.IsType<PgColumnConfigurationOperation.SetStatistics>(configurations[0]).Target);
        Assert.IsType<PgColumnConfigurationOperation.SetDefaultStatistics>(configurations[1]);
        Identifier compression = Assert.IsType<PgColumnConfigurationOperation.SetCompression>(configurations[2]).Method;
        Assert.Equal("default", compression.Name);
        Assert.Equal(QuoteStyle.Ansi, compression.QuoteStyle);
        Assert.IsType<PgColumnConfigurationOperation.SetDefaultCompression>(configurations[3]);
    }

    [Fact]
    public void AlterTableColumns_PreservesSourcesCommentsAndStatementBoundaries()
    {
        const string sql = "/* lead */ ALTER TABLE IF EXISTS ONLY \"App\".\"Items\" ALTER COLUMN \"Label\" SET DATA TYPE \"App\".\"LabelType\"[] COLLATE \"App\".\"Collation\" USING \"Label\" /* tail */; ALTER TABLE items DROP IF EXISTS old_label RESTRICT; SELECT 1";
        var statements = ParseStatements(sql, sourceId: 179);
        var table = Assert.IsType<AlterTable>(statements[0]);
        var column = Assert.IsType<AlterTableOperation.PgColumn>(Assert.Single(table.Operations));
        var change = Assert.IsType<PgAlterColumnOperation.ChangeType>(column.Operation);
        var namedType = Assert.IsType<DataTypeReference.Named>(Assert.IsType<DataTypeReference.Array>(change.DataType).ElementType);

        Assert.Equal(3, statements.Count);
        Assert.Equal(0, table.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, table.Meta.EndPlusTerminatorLength);
        Assert.All(table.Table.Values, identifier => Assert.Equal(179, identifier.Source!.Value.SourceId));
        Assert.Equal(179, column.Column.Source!.Value.SourceId);
        Assert.All(namedType.Name.Values, identifier => Assert.Equal(179, identifier.Source!.Value.SourceId));
        Assert.All(change.Collation!.Values, identifier => Assert.Equal(179, identifier.Source!.Value.SourceId));
        Assert.Equal(179, Assert.IsType<SingleIdentifier>(change.Using).Identifier.Source!.Value.SourceId);
        Assert.Contains(table.Meta.PreNonSql!, trivia => trivia is NonSql.BlockComment { Body: " lead " });
        Assert.Contains(table.Meta.PostNonSql!, trivia => trivia is NonSql.BlockComment { Body: " tail " });
        Assert.IsType<AlterTable>(statements[1]);
        Assert.IsType<Select>(statements[2]);
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void AlterTableColumns_PreservesSqlAtomicBody()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC ALTER TABLE items ADD COLUMN IF NOT EXISTS label TEXT, ALTER label SET NOT NULL, ALTER label SET STATISTICS DEFAULT; ALTER TABLE items ALTER label TYPE app.label[], ALTER id SET GENERATED ALWAYS RESTART 10; END";
        var statement = Assert.IsType<CreateProcedure>(ParseStatement(sql));

        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<CreateProcedure>(ParseStatement(formatted));
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void AlterTableColumns_ReportsWrittenTypesAndExpressionsAndRestoresScopes()
    {
        const string sql = "ALTER TABLE app.items ADD COLUMN IF NOT EXISTS label app.label[] COLLATE app.add_collation, ALTER label TYPE app.new_label[] COLLATE app.collation USING app.convert(label), ALTER generated SET EXPRESSION AS (app.compute(label)), ALTER label SET DEFAULT (app.make_label()), ALTER label RESET (n_distinct), DROP COLUMN old_label CASCADE";
        var statement = Assert.IsType<AlterTable>(ParseStatement(sql));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var tables = new PseudoTableSet(schema,
        [
            new PseudoTable("items", new ObjectIdentifier("items", schema), ["label"], PseudoTableType.Table),
            new PseudoTable("label", new ObjectIdentifier("label", schema), [], PseudoTableType.Table)
        ]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        context.AddLocalVariable(new Identifier("label"));
        using var scope = context.Enter(ReferencedItemsContext.WhereClause);

        ItemRef[] references = [.. statement.GetReferencedItems(context)];

        Assert.Equal(["app.items", "app.label", "app.add_collation", "app.new_label", "app.collation", "app.convert", "app.compute", "app.make_label"],
            references.OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql()));
        Assert.Equal(ItemType.Table, references[0].Type);
        Assert.All(references.Skip(1), reference => Assert.Equal(ItemType.Unknown, reference.Type));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Single(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Table, PseudoTables = tables }));
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        _ = statement.GetReferencedItems(context).Take(2).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        _ = statement.GetReferencedItems(context).Take(5).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        Assert.Same(tables, context.PseudoTables);
    }

    [Fact]
    public void AlterTableManagement_PreservesTypedPayloadsAndSources()
    {
        const string sql = "/* lead */ ALTER TABLE IF EXISTS ONLY \"App\".\"Items\" SET UNLOGGED, INHERIT \"App\".\"Parent\", NO INHERIT app.old_parent, OF \"App\".\"RowType\", NOT OF, OWNER TO \"Role\" /* tail */; ALTER TABLE items SET SCHEMA archive; SELECT 1";
        var statements = ParseStatements(sql, sourceId: 181);
        var statement = Assert.IsType<AlterTable>(statements[0]);
        var inherit = Assert.IsType<AlterTableOperation.PgInherit>(statement.Operations[1]);
        var of = Assert.IsType<AlterTableOperation.PgOf>(statement.Operations[3]);
        var owner = Assert.IsType<PgAlterRelationOperation.OwnerTo>(Assert.IsType<AlterTableOperation.PgRelation>(statement.Operations[5]).Operation);

        Assert.Equal(3, statements.Count);
        Assert.True(statement.IfExists);
        Assert.True(statement.Only);
        Assert.False(Assert.IsType<AlterTableOperation.PgPersistence>(statement.Operations[0]).Logged);
        Assert.False(inherit.No);
        Assert.True(Assert.IsType<AlterTableOperation.PgInherit>(statement.Operations[2]).No);
        Assert.IsType<AlterTableOperation.PgNotOf>(statement.Operations[4]);
        Assert.Equal("\"Role\"", owner.Owner.ToSql());
        Assert.All(inherit.Parent.Values, identifier => Assert.Equal(181, identifier.Source!.Value.SourceId));
        Assert.All(of.Type.Values, identifier => Assert.Equal(181, identifier.Source!.Value.SourceId));
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is NonSql.BlockComment { Body: " tail " });
        Assert.IsType<AlterTable>(statements[1]);
        Assert.IsType<Select>(statements[2]);
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void AlterTableManagement_ReportsParentTablesAndUnresolvedTypesAndRestoresScopes()
    {
        const string sql = "ALTER TABLE app.items INHERIT app.parent, NO INHERIT app.old_parent, OF app.row_type, NOT OF, OWNER TO role_name, SET TABLESPACE fast";
        var statement = Assert.IsType<AlterTable>(ParseStatement(sql));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var tables = new PseudoTableSet(schema,
        [
            new PseudoTable("items", new ObjectIdentifier("items", schema), [], PseudoTableType.Table),
            new PseudoTable("parent", new ObjectIdentifier("parent", schema), [], PseudoTableType.Table),
            new PseudoTable("old_parent", new ObjectIdentifier("old_parent", schema), [], PseudoTableType.Table),
            new PseudoTable("row_type", new ObjectIdentifier("row_type", schema), [], PseudoTableType.Table)
        ]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        context.AddLocalVariable(new Identifier("row_type"));
        using var scope = context.Enter(ReferencedItemsContext.WhereClause);

        ItemRef[] references = [.. statement.GetReferencedItems(context)];

        Assert.Equal(["app.items", "app.parent", "app.old_parent", "app.row_type"], references.OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql()));
        Assert.All(references.Take(3), reference => Assert.Equal(ItemType.Table, reference.Type));
        Assert.Equal(ItemType.Unknown, references[3].Type);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Equal(3, statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Table, PseudoTables = tables }).Count());
        _ = statement.GetReferencedItems(context).Take(2).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        _ = statement.GetReferencedItems(context).Take(4).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        Assert.Same(tables, context.PseudoTables);
    }

    [Fact]
    public void AlterTableManagement_RoundTripsAtomicBody()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC ALTER TABLE items OWNER TO CURRENT_ROLE, SET LOGGED, SET (fillfactor = 70), INHERIT app.parent; ALTER TABLE items SET SCHEMA archive; END";
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
    [InlineData("ALTER TABLE items SET SCHEMA archive, SET LOGGED")]
    [InlineData("ALTER TABLE items SET LOGGED, SET SCHEMA archive")]
    [InlineData("ALTER TABLE ONLY items SET SCHEMA archive")]
    [InlineData("ALTER TABLE ONLY items RENAME TO new_items")]
    [InlineData("ALTER TABLE items OWNER role_name")]
    [InlineData("ALTER TABLE items OWNER TO")]
    [InlineData("ALTER TABLE items OWNER TO 'role_name'")]
    [InlineData("ALTER TABLE items OWNER TO role_name@host")]
    [InlineData("ALTER TABLE items SET SCHEMA app.archive")]
    [InlineData("ALTER TABLE items SET TABLESPACE")]
    [InlineData("ALTER TABLE items SET TABLESPACE fast NOWAIT")]
    [InlineData("ALTER TABLE items SET ACCESS METHOD")]
    [InlineData("ALTER TABLE items CLUSTER items_idx")]
    [InlineData("ALTER TABLE items SET WITHOUT")]
    [InlineData("ALTER TABLE items SET ()")]
    [InlineData("ALTER TABLE items SET (app.toast.option = 1)")]
    [InlineData("ALTER TABLE items RESET ()")]
    [InlineData("ALTER TABLE items RESET (fillfactor = 70)")]
    [InlineData("ALTER TABLE items SET LOGGED UNLOGGED")]
    [InlineData("ALTER TABLE items INHERIT")]
    [InlineData("ALTER TABLE items NO INHERIT")]
    [InlineData("ALTER TABLE items OF")]
    [InlineData("ALTER TABLE items NOT OF app.row_type")]
    [InlineData("ALTER TABLE items DEPENDS ON EXTENSION ext")]
    public void AlterTableManagement_RejectsMalformedAndStandaloneCombinations(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("ALTER TABLE items ADD COLUMN IF EXISTS label TEXT")]
    [InlineData("ALTER TABLE items DROP COLUMN IF NOT EXISTS label")]
    [InlineData("ALTER TABLE items DROP COLUMN label CASCADE RESTRICT")]
    [InlineData("ALTER TABLE items RENAME label TO new_label, ADD COLUMN other_label TEXT")]
    [InlineData("ALTER TABLE items ADD COLUMN other_label TEXT, RENAME COLUMN label TO new_label")]
    [InlineData("ALTER TABLE items ADD COLUMN label TEXT FIRST")]
    [InlineData("ALTER TABLE items ADD COLUMN label TEXT AFTER other_label")]
    [InlineData("ALTER TABLE items MODIFY COLUMN label TEXT")]
    [InlineData("ALTER TABLE items CHANGE COLUMN label other_label TEXT")]
    [InlineData("ALTER TABLE items AUTO_INCREMENT = 10")]
    [InlineData("ALTER TABLE items DEFAULT CHARACTER SET utf8")]
    [InlineData("ALTER TABLE items RENAME AS new_items")]
    [InlineData("ALTER TABLE items RENAME new_items")]
    [InlineData("ALTER TABLE items RENAME COLUMN label TO app.new_label")]
    [InlineData("ALTER TABLE items ALTER label TYPE")]
    [InlineData("ALTER TABLE items ALTER label SET DATA TEXT")]
    [InlineData("ALTER TABLE items ALTER label SET TYPE TEXT")]
    [InlineData("ALTER TABLE items ALTER label TYPE TEXT COLLATE")]
    [InlineData("ALTER TABLE items ALTER label TYPE TEXT USING")]
    [InlineData("ALTER TABLE items ALTER label SET NOT")]
    [InlineData("ALTER TABLE items ALTER label DROP NULL")]
    [InlineData("ALTER TABLE items ALTER label SET EXPRESSION AS label")]
    [InlineData("ALTER TABLE items ALTER label SET EXPRESSION (label)")]
    [InlineData("ALTER TABLE items ALTER label DROP EXPRESSION IF NOT EXISTS")]
    [InlineData("ALTER TABLE items ALTER label SET STATISTICS")]
    [InlineData("ALTER TABLE items ALTER label SET STATISTICS 1.5")]
    [InlineData("ALTER TABLE items ALTER label SET STATISTICS 2147483648")]
    [InlineData("ALTER TABLE items ALTER label SET ()")]
    [InlineData("ALTER TABLE items ALTER label SET (n_distinct)")]
    [InlineData("ALTER TABLE items ALTER label SET (app.n_distinct = 1)")]
    [InlineData("ALTER TABLE items ALTER label RESET ()")]
    [InlineData("ALTER TABLE items ALTER label RESET (app.n_distinct)")]
    [InlineData("ALTER TABLE items ALTER label SET STORAGE INVALID")]
    [InlineData("ALTER TABLE items ALTER label SET STORAGE \"EXTENDED\"")]
    [InlineData("ALTER TABLE items ALTER label SET COMPRESSION")]
    [InlineData("ALTER TABLE items ALTER label SET NOT NULL SET STATISTICS 100")]
    [InlineData("ALTER TABLE items ALTER label SET DEFAULT")]
    [InlineData("ALTER TABLE items ALTER label SET DEFAULT 1 +")]
    [InlineData("ALTER TABLE items ALTER label SET NOT NULL,")]
    public void AlterTableColumns_RejectMalformedOrCombinedStandaloneActions(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
