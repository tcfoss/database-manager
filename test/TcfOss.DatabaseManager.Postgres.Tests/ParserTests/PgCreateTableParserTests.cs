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

public class PgCreateTableParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("CREATE TABLE items (id INT)")]
    [InlineData("CREATE TEMP TABLE items (id INT)")]
    [InlineData("CREATE TEMPORARY TABLE IF NOT EXISTS items (id INT)")]
    [InlineData("CREATE UNLOGGED TABLE app.items (id INT)")]
    [InlineData("CREATE UNLOGGED TABLE IF NOT EXISTS app.items (id INT)")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT PRESERVE ROWS")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT DELETE ROWS")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT DROP")]
    [InlineData("CREATE TEMPORARY TABLE items (id INT) ON COMMIT PRESERVE ROWS")]
    [InlineData("CREATE TEMPORARY TABLE items (id INT) ON COMMIT DELETE ROWS")]
    [InlineData("CREATE TEMPORARY TABLE items (id INT) ON COMMIT DROP")]
    [InlineData("CREATE TEMP TABLE items AS SELECT 1")]
    [InlineData("CREATE TEMPORARY TABLE items ON COMMIT PRESERVE ROWS AS SELECT id FROM app.source_items")]
    [InlineData("CREATE TEMP TABLE items ON COMMIT DELETE ROWS AS SELECT id FROM app.source_items")]
    [InlineData("CREATE TEMP TABLE items ON COMMIT DROP AS SELECT id FROM app.source_items")]
    [InlineData("CREATE UNLOGGED TABLE app.items AS SELECT id FROM app.source_items")]
    [InlineData("CREATE TEMP TABLE \"Work; DROP TABLE items\" (\"ON COMMIT\" INT) ON COMMIT DROP")]
    public void TablePersistence_RoundTripsSqlJsonAndFormatting(string sql)
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<CreateTable>(ParseStatement(formatted));
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("", null, false, null)]
    [InlineData("TEMP ", "TEMP", false, null)]
    [InlineData("TEMPORARY ", "TEMPORARY", false, null)]
    [InlineData("UNLOGGED ", null, true, null)]
    [InlineData("TEMP ", "TEMP", false, "PRESERVE ROWS")]
    [InlineData("TEMPORARY ", "TEMPORARY", false, "DELETE ROWS")]
    [InlineData("TEMP ", "TEMP", false, "DROP")]
    public void TablePersistence_PreservesTypedOptionsAndOmittedDefaults(string prefix, string? temporary, bool unlogged, string? onCommit)
    {
        string suffix = onCommit == null ? "" : $" ON COMMIT {onCommit}";
        var statement = Assert.IsType<CreateTable>(ParseStatement($"CREATE {prefix}TABLE items (id INT){suffix}"));

        Assert.Equal(temporary, statement.Temporary?.ToSql());
        if (!unlogged && onCommit == null)
        {
            Assert.Null(statement.PgOptions);
        }
        else
        {
            PgCreateTableOptions options = Assert.IsType<PgCreateTableOptions>(statement.PgOptions);
            Assert.Equal(unlogged, options.Unlogged);
            Assert.Equal(onCommit == null ? null : PgOnCommit.Parse(onCommit), options.OnCommit);
        }
    }

    [Fact]
    public void TablePersistence_NormalizesKeywordCase()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("create temporary table items (id int) on commit preserve rows"));

        Assert.Equal("CREATE TEMPORARY TABLE items (id INT) ON COMMIT PRESERVE ROWS", statement.ToSql());
        Assert.Equal(PgOnCommit.PreserveRows, statement.PgOptions!.OnCommit);
    }

    [Fact]
    public void TablePersistence_PreservesSourcesCommentsAndStatementBoundaries()
    {
        const string sql = "/* lead */ CREATE TEMPORARY TABLE IF NOT EXISTS \"Work\" (\"Id\" INT) ON COMMIT DELETE ROWS /* tail */; CREATE UNLOGGED TABLE app.archive (id INT); SELECT 1";
        var statements = ParseStatements(sql, sourceId: 193);
        var temporary = Assert.IsType<CreateTable>(statements[0]);
        var unlogged = Assert.IsType<CreateTable>(statements[1]);

        Assert.Equal(3, statements.Count);
        Assert.True(temporary.IfNotExists);
        Assert.Equal("TEMPORARY", temporary.Temporary!.ToSql());
        Assert.Equal(PgOnCommit.DeleteRows, temporary.PgOptions!.OnCommit);
        Assert.False(temporary.PgOptions.Unlogged);
        Assert.True(unlogged.PgOptions!.Unlogged);
        Assert.Null(unlogged.PgOptions.OnCommit);
        Assert.Equal(0, temporary.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, temporary.Meta.EndPlusTerminatorLength);
        Assert.All(temporary.Name.Values, identifier => Assert.Equal(193, identifier.Source!.Value.SourceId));
        Assert.Equal(193, Assert.Single(temporary.Columns).Name.Source!.Value.SourceId);
        Assert.Contains(temporary.Meta.PreNonSql!, trivia => trivia is NonSql.BlockComment { Body: " lead " });
        Assert.Contains(temporary.Meta.PostNonSql!, trivia => trivia is NonSql.BlockComment { Body: " tail " });
        Assert.IsType<Select>(statements[2]);
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("CREATE TEMP TABLE stage ON COMMIT DROP AS SELECT * FROM app.items")]
    [InlineData("CREATE UNLOGGED TABLE stage AS SELECT * FROM app.items")]
    public void TablePersistence_KeepsCtasQueryReferencesAndCallerScopes(string sql)
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        context.AddLocalVariable(new Identifier("stage"));
        using var scope = context.Enter(ReferencedItemsContext.WhereClause);

        var reference = Assert.IsType<ItemRef.ObjectNameRef>(Assert.Single(statement.GetReferencedItems(context)));

        Assert.Equal("app.items", reference.Name.ToSql());
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        _ = statement.GetReferencedItems(context).Take(1).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        var declaration = Assert.IsType<CreateTable>(ParseStatement("CREATE UNLOGGED TABLE stage (id INT)"));
        Assert.Empty(declaration.GetReferencedItems(context));
    }

    [Fact]
    public void TablePersistence_RoundTripsAtomicBodyAndFollowingStatement()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC CREATE TEMP TABLE stage (id INT) ON COMMIT DROP; CREATE UNLOGGED TABLE archive AS SELECT id FROM app.items; SELECT 1; END; SELECT 2";
        var statements = ParseStatements(sql);
        var procedure = Assert.IsType<CreateProcedure>(statements[0]);

        Assert.Equal(2, statements.Count);
        Assert.IsType<Select>(statements[1]);
        Assert.Equal(procedure, ParseStatement(procedure.ToSql()));
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(2, ParseStatements(formatted).Count);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT) ON COMMIT DROP")]
    [InlineData("CREATE UNLOGGED TABLE items (id INT) ON COMMIT DELETE ROWS")]
    public void TablePersistence_LeavesTemporaryEligibilityToSemanticValidation(string sql)
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));

        Assert.Null(statement.Temporary);
        Assert.NotNull(statement.PgOptions!.OnCommit);
        Assert.Equal(sql, statement.ToSql());
    }

    [Theory]
    [InlineData("CREATE TEMP TABLE items (id INT) ON")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT PRESERVE")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT DELETE")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT ROWS")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT KEEP ROWS")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT \"DROP\"")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT 'DROP'")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT DROP ROWS")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT PRESERVE ROW")]
    [InlineData("CREATE TEMP TABLE items (id INT) ON COMMIT DROP ON COMMIT DELETE ROWS")]
    [InlineData("CREATE TEMP TABLE items AS SELECT 1 ON COMMIT DROP")]
    [InlineData("CREATE TEMP UNLOGGED TABLE items (id INT)")]
    [InlineData("CREATE UNLOGGED TEMP TABLE items (id INT)")]
    [InlineData("CREATE TEMP TEMPORARY TABLE items (id INT)")]
    [InlineData("CREATE UNLOGGED UNLOGGED TABLE items (id INT)")]
    [InlineData("CREATE LOGGED TABLE items (id INT)")]
    [InlineData("CREATE OR REPLACE UNLOGGED TABLE items (id INT)")]
    [InlineData("CREATE UNLOGGED VIEW items AS SELECT 1")]
    [InlineData("CREATE UNLOGGED INDEX items_idx ON items (id)")]
    [InlineData("CREATE GLOBAL TEMP TABLE items (id INT)")]
    [InlineData("CREATE LOCAL TEMPORARY TABLE items (id INT)")]
    [InlineData("CREATE TABLE items (LIKE app.source_items INCLUDING ALL)")]
    [InlineData("CREATE TABLE items (id INT) INHERITS (app.parent)")]
    [InlineData("CREATE TEMP TABLE items (id) AS SELECT 1")]
    [InlineData("CREATE TEMP TABLE items AS SELECT 1 WITH NO DATA")]
    [InlineData("CREATE UNLOGGED TABLE items AS SELECT 1 WITH DATA")]
    public void TablePersistence_RejectsMalformedAndDeferredForms(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id)")]
    [InlineData("CREATE TABLE items (id INT, region INT) PARTITION BY RANGE (id, region)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY LIST (id)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY HASH (id)")]
    [InlineData("CREATE TABLE items () PARTITION BY RANGE (id)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE ((id + 1))")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY HASH (app.bucket(id))")]
    [InlineData("CREATE TABLE items (label TEXT) PARTITION BY LIST (label COLLATE \"C\" app.text_ops)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE ((app.bucket(id) + 1) app.int_ops)")]
    [InlineData("CREATE TEMP TABLE items (id INT) PARTITION BY RANGE (id) ON COMMIT DROP")]
    [InlineData("CREATE UNLOGGED TABLE items (id INT) PARTITION BY HASH (id)")]
    [InlineData("CREATE TABLE IF NOT EXISTS \"App\".\"Items\" (\"Id\" INT) PARTITION BY RANGE (\"Id\")")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent FOR VALUES IN (1, 2, NULL)")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent FOR VALUES IN ('MINVALUE', 'MAXVALUE')")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent FOR VALUES IN (app.make_value(1))")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent FOR VALUES FROM (MINVALUE) TO (MAXVALUE)")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent FOR VALUES FROM (1, MINVALUE) TO (5, MAXVALUE)")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent FOR VALUES FROM (-10) TO (app.make_value(1) + 10)")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent FOR VALUES WITH (MODULUS 4, REMAINDER 1)")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent FOR VALUES WITH (REMAINDER 1, MODULUS 4)")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent DEFAULT")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent () DEFAULT")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent (id) DEFAULT")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent (id WITH OPTIONS) DEFAULT")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent (id DEFAULT 1 NOT NULL, label WITH OPTIONS DEFAULT 'x') DEFAULT")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent (CONSTRAINT positive CHECK (id > 0)) DEFAULT")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent (id DEFAULT (app.next_value()), CONSTRAINT positive CHECK (app.valid(id))) FOR VALUES IN (1)")]
    [InlineData("CREATE TABLE child PARTITION OF app.parent FOR VALUES FROM (1) TO (10) PARTITION BY LIST (region)")]
    [InlineData("CREATE TEMPORARY TABLE child PARTITION OF parent DEFAULT PARTITION BY HASH (id) ON COMMIT DELETE ROWS")]
    [InlineData("CREATE TABLE \"Child; DROP TABLE items\" PARTITION OF \"Parent; SELECT 1\" DEFAULT")]
    public void TablePartitions_RoundTripsSqlJsonAndFormatting(string sql)
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        var formattedStatement = Assert.IsType<CreateTable>(ParseStatement(formatted));
        Assert.Equal(statement.PgOptions?.PartitionBy?.Strategy, formattedStatement.PgOptions?.PartitionBy?.Strategy);
        Assert.Equal(statement.PgOptions?.PartitionOf?.Bound.GetType(), formattedStatement.PgOptions?.PartitionOf?.Bound.GetType());
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void TablePartitions_PreservesTypedKeysOverridesAndBounds()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE child PARTITION OF app.parent (id WITH OPTIONS DEFAULT 1) FOR VALUES FROM (MINVALUE, 1) TO (MAXVALUE, 9) PARTITION BY RANGE ((id + 1), label COLLATE \"C\" app.text_ops)"));
        PgCreateTableOptions options = statement.PgOptions!;
        var range = Assert.IsType<PgPartitionBound.RangeValues>(options.PartitionOf!.Bound);
        var column = Assert.IsType<StatementColumn.PgPartitionOverride>(Assert.Single(statement.Columns));

        Assert.Equal("app.parent", options.PartitionOf.Parent.ToSql());
        Assert.Equal(PgPartitionStrategy.Range, options.PartitionBy!.Strategy);
        Assert.IsType<Nested>(options.PartitionBy.Keys[0].Expression);
        Assert.IsType<Collate>(options.PartitionBy.Keys[1].Expression);
        Assert.Equal("app.text_ops", options.PartitionBy.Keys[1].OperatorClass!.ToSql());
        Assert.IsType<PgPartitionBound.RangeValue.MinValue>(range.From[0]);
        Assert.IsType<PgPartitionBound.RangeValue.MaxValue>(range.To[0]);
        Assert.IsType<PgPartitionBound.RangeValue.ExpressionValue>(range.From[1]);
        Assert.True(column.WithOptions);
        Assert.IsType<StatementColumnOption.Default.DefaultValue>(Assert.Single(column.Options));
        Assert.False(options.Unlogged);
        Assert.Null(options.OnCommit);
    }

    [Theory]
    [InlineData("MODULUS 4, REMAINDER 1", false)]
    [InlineData("REMAINDER 1, MODULUS 4", true)]
    public void TablePartitions_PreservesHashFieldOrder(string fields, bool remainderFirst)
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE child PARTITION OF parent FOR VALUES WITH ({fields})"));
        var bound = Assert.IsType<PgPartitionBound.HashValues>(statement.PgOptions!.PartitionOf!.Bound);

        Assert.Equal(4, bound.Modulus);
        Assert.Equal(1, bound.Remainder);
        Assert.Equal(remainderFirst, bound.RemainderFirst);
    }

    [Fact]
    public void TablePartitions_PreservesSourcesCommentsAndStatementBoundaries()
    {
        const string sql = "/* lead */ CREATE TABLE IF NOT EXISTS \"App\".\"Child\" PARTITION OF \"App\".\"Parent\" (\"Id\" WITH OPTIONS DEFAULT 1) FOR VALUES IN (1) PARTITION BY LIST (\"Id\" app.int_ops) /* tail */; SELECT 1";
        var statements = ParseStatements(sql, sourceId: 197);
        var statement = Assert.IsType<CreateTable>(statements[0]);
        PgCreateTableOptions options = statement.PgOptions!;

        Assert.Equal(2, statements.Count);
        Assert.True(statement.IfNotExists);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.All(options.PartitionOf!.Parent.Values, identifier => Assert.Equal(197, identifier.Source!.Value.SourceId));
        Assert.Equal(197, Assert.Single(statement.Columns).Name.Source!.Value.SourceId);
        Assert.Equal(197, Assert.IsType<SingleIdentifier>(Assert.Single(options.PartitionBy!.Keys).Expression).Identifier.Source!.Value.SourceId);
        Assert.All(Assert.Single(options.PartitionBy.Keys).OperatorClass!.Values, identifier => Assert.Equal(197, identifier.Source!.Value.SourceId));
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is NonSql.BlockComment { Body: " tail " });
        Assert.IsType<Select>(statements[1]);
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void TablePartitions_ReportsParentAndExpressionFunctionsWithoutCapturingCallerNames()
    {
        const string sql = "CREATE TABLE child PARTITION OF app.parent (id DEFAULT app.next_value(), CONSTRAINT valid CHECK (app.valid(id))) FOR VALUES IN (app.bound_value()) PARTITION BY RANGE ((app.bucket(id) + 1), label COLLATE app.casefold app.text_ops)";
        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var tables = new PseudoTableSet(schema,
        [
            new PseudoTable("parent", new ObjectIdentifier("parent", schema), [], PseudoTableType.Table)
        ]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        context.AddLocalVariable(new Identifier("parent"));
        context.AddLocalVariable(new Identifier("id"));
        using var scope = context.Enter(ReferencedItemsContext.WhereClause);

        ItemRef[] references = [.. statement.GetReferencedItems(context)];

        Assert.Equal(["app.parent", "app.bucket", "app.bound_value", "app.next_value", "app.valid"], references.OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql()));
        Assert.Equal(ItemType.Table, references[0].Type);
        Assert.DoesNotContain(references, reference => reference.Type == ItemType.Variable);
        Assert.DoesNotContain(references.OfType<ItemRef.ObjectNameRef>(), reference => reference.Name.ToSql() is "app.casefold" or "app.text_ops");
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Single(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Table, PseudoTables = tables }));
        _ = statement.GetReferencedItems(context).Take(1).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        _ = statement.GetReferencedItems(context).Take(2).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        Assert.Same(tables, context.PseudoTables);
    }

    [Fact]
    public void TablePartitions_RoundTripsAtomicBodyAndFollowingStatement()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC CREATE TABLE items (id INT) PARTITION BY RANGE (id); CREATE TABLE child PARTITION OF items DEFAULT PARTITION BY HASH (id); SELECT 1; END; SELECT 2";
        var statements = ParseStatements(sql);
        var procedure = Assert.IsType<CreateProcedure>(statements[0]);

        Assert.Equal(2, statements.Count);
        Assert.Equal(procedure, ParseStatement(procedure.ToSql()));
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(2, ParseStatements(formatted).Count);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES FROM (1, MINVALUE) TO (2)")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES WITH (MODULUS 0, REMAINDER -1)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY LIST (id, missing_column)")]
    public void TablePartitions_LeavesBoundAndKeyEligibilityToSemanticValidation(string sql)
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));

        Assert.NotNull(statement.PgOptions);
        Assert.Equal(sql, statement.ToSql());
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT) PARTITION")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY \"RANGE\" (id)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY INVALID (id)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE ()")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id,)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id + 1)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (app.id)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (1)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id::INT)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id DESC)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id NULLS LAST)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (app.bucket(id) OVER ())")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (app.bucket(id) FILTER (WHERE TRUE))")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id app.int_ops(option = 1))")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id) PARTITION BY LIST (id)")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id) AS SELECT 1")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id) SELECT 1")]
    [InlineData("CREATE TABLE child PARTITION OF")]
    [InlineData("CREATE TABLE child PARTITION OF parent")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES IN ()")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES IN (1,)")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES FROM (1)")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES FROM () TO (2)")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES FROM (1) TO ()")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES WITH (MODULUS 4)")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES WITH (REMAINDER 1)")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES WITH (MODULUS 4, MODULUS 1)")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES WITH (MODULUS 4, REMAINDER 1, REMAINDER 2)")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES WITH (MODULUS 4.5, REMAINDER 1)")]
    [InlineData("CREATE TABLE child PARTITION OF parent FOR VALUES WITH (MODULUS 2147483648, REMAINDER 1)")]
    [InlineData("CREATE TABLE child PARTITION OF parent DEFAULT DEFAULT")]
    [InlineData("CREATE TABLE child PARTITION OF parent (id INT) DEFAULT")]
    [InlineData("CREATE TABLE child PARTITION OF parent DEFAULT AS SELECT 1")]
    [InlineData("CREATE TABLE child PARTITION OF parent DEFAULT SELECT 1")]
    [InlineData("CREATE TABLE child PARTITION OF 'parent' DEFAULT")]
    [InlineData("CREATE TABLE items (id INT) PARTITION BY RANGE (id) INHERITS (parent)")]
    [InlineData("ALTER TABLE parent ATTACH PARTITION child DEFAULT")]
    [InlineData("ALTER TABLE parent DETACH PARTITION child")]
    public void TablePartitions_RejectsMalformedAndOutOfSliceForms(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
