using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
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

public class PgCreateIndexParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("CREATE INDEX items_idx ON items (id)")]
    [InlineData("CREATE INDEX ON items (id)")]
    [InlineData("CREATE UNIQUE INDEX ON items (id)")]
    [InlineData("CREATE INDEX CONCURRENTLY ON ONLY app.items (id)")]
    [InlineData("CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS items_idx ON items (id)")]
    [InlineData("CREATE INDEX items_idx ON items (label COLLATE app.casefold)")]
    [InlineData("CREATE INDEX items_idx ON items (label app.text_ops)")]
    [InlineData("CREATE INDEX items_idx ON items (label COLLATE app.casefold app.text_ops ASC NULLS FIRST)")]
    [InlineData("CREATE INDEX items_idx ON items (id DESC NULLS LAST, label ASC NULLS FIRST)")]
    [InlineData("CREATE INDEX items_idx ON items ((id + 1) app.int_ops DESC NULLS FIRST)")]
    [InlineData("CREATE INDEX items_idx ON items (app.compute(id) app.int_ops (bits = 4) NULLS LAST)")]
    [InlineData("CREATE INDEX items_idx ON items (id(5))")]
    [InlineData("CREATE INDEX items_idx ON items (id) INCLUDE (label, payload)")]
    [InlineData("CREATE UNIQUE INDEX items_idx ON items (id) NULLS DISTINCT")]
    [InlineData("CREATE UNIQUE INDEX items_idx ON items (id) NULLS NOT DISTINCT")]
    [InlineData("CREATE INDEX items_idx ON items (id) WITH (fillfactor = 80)")]
    [InlineData("CREATE INDEX items_idx ON items (id) WITH (custom_parameter = 4, fastupdate = 'off')")]
    [InlineData("CREATE INDEX items_idx ON items (id) TABLESPACE fast")]
    [InlineData("CREATE INDEX items_idx ON items (id) WHERE id > 0")]
    [InlineData("CREATE INDEX items_idx ON items (id) WITH (fillfactor = 80) TABLESPACE fast WHERE id > 0")]
    [InlineData("CREATE INDEX items_idx ON items USING bloom (id)")]
    [InlineData("CREATE INDEX items_idx ON items USING \"btree\" (id)")]
    [InlineData("CREATE INDEX \"Index; SELECT 1\" ON \"Table; DROP TABLE items\" (\"Column\" \"Ops; SELECT 2\" (\"Option\" = 1)) TABLESPACE \"Space; SELECT 3\"")]
    [InlineData("CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS items_idx ON ONLY app.items USING BTREE (label COLLATE app.casefold app.text_ops (version = 1) DESC NULLS LAST, (id + 1) ASC NULLS FIRST) INCLUDE (payload) NULLS NOT DISTINCT WITH (fillfactor = 70, deduplicate_items = 'off') TABLESPACE fast WHERE app.keep(id)")]
    public void ExpandedIndexes_RoundTripsSqlJsonAndFormatting(string sql)
    {
        var statement = Assert.IsType<CreateIndex>(ParseStatement(sql), exactMatch: false);

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<CreateIndex>(ParseStatement(formatted), exactMatch: false);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
        Assert.Contains("\"", formatted);
    }

    [Theory]
    [InlineData("BTREE")]
    [InlineData("HASH")]
    [InlineData("GIST")]
    [InlineData("SPGIST")]
    [InlineData("GIN")]
    [InlineData("BRIN")]
    public void ExpandedIndexes_ReusesBuiltInMethods(string method)
    {
        var statement = Assert.IsType<CreateIndex.Standard>(ParseStatement($"CREATE INDEX items_idx ON items USING {method} (id)"));

        Assert.Equal(BuiltInIndexMethod.Parse(method), Assert.IsType<IndexMethod.BuiltIn>(statement.IndexMethod).Method);
        Assert.True(statement.IndexMethodAfterTableName);
        Assert.Null(statement.PgOptions);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Fact]
    public void ExpandedIndexes_DirectDdlParserRejectsReplacement()
    {
        var parser = new PgParser();
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize("INDEX items_idx ON items (id)")]);

        var exception = Assert.ThrowsAny<ParseException>(() => parser.DdlParser.ParseCreateIndex(state,
            Core.Statements.LabelAttributes.CreateOrLabel.OrReplace));

        Assert.Contains("CREATE [UNIQUE] INDEX", exception.Message);
    }

    [Fact]
    public void ExpandedIndexes_PreservesTypedOptionsAndOmittedDefaults()
    {
        const string sql = "CREATE UNIQUE INDEX CONCURRENTLY items_idx ON ONLY items USING bloom (label app.text_ops (bits = 4) ASC NULLS FIRST) INCLUDE (payload) NULLS NOT DISTINCT WITH (fillfactor = 80) TABLESPACE fast WHERE label IS NOT NULL";
        var statement = Assert.IsType<CreateIndex.Unique>(ParseStatement(sql));
        PgCreateIndexOptions options = Assert.IsType<PgCreateIndexOptions>(statement.PgOptions);
        var key = Assert.IsType<KeyPart.Column>(Assert.Single(statement.Columns));
        PgIndexKeyOptions keyOptions = Assert.IsType<PgIndexKeyOptions>(key.PgOptions);

        Assert.True(options.Concurrently);
        Assert.True(options.Only);
        Assert.False(options.NullsDistinct);
        Assert.Equal("bloom", Assert.IsType<IndexMethod.Custom>(statement.IndexMethod).Method.Name);
        Assert.Equal("fast", options.Tablespace!.Name);
        Assert.Equal("fillfactor", Assert.Single(options.StorageParameters!).Name.ToSql());
        Assert.Null(statement.Options);
        Assert.Equal("app.text_ops", keyOptions.OperatorClass!.ToSql());
        Assert.Equal("bits", Assert.Single(keyOptions.OperatorClassParameters!).Name.ToSql());
        Assert.Equal(NullsOrder.First, keyOptions.NullsOrder);
        Assert.Equal(Direction.Ascending, key.Direction);
        Assert.Equal(key.PgOptions, Assert.Single(KeyPart.RemoveOptionalElementsFromKeyParts(statement.Columns)).PgOptions);

        var plain = Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX ON items (id)"));
        Assert.Null(plain.Name);
        Assert.Null(plain.PgOptions);
        Assert.Null(Assert.Single(plain.Columns).PgOptions);
    }

    [Fact]
    public void ExpandedIndexes_PreservesNumericArgumentFunctionAsExpression()
    {
        var statement = Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX items_idx ON items (id(5))"));

        Assert.IsType<KeyPart.IndexExpression>(Assert.Single(statement.Columns));
    }

    [Fact]
    public void ExpandedIndexes_NormalizesBareParameterValues()
    {
        var statement = Assert.IsType<CreateIndex.Standard>(ParseStatement("create index items_idx on items using gin (id app.int_ops (mode = off)) with (fastupdate = off)"));

        Assert.Equal("CREATE INDEX items_idx ON items USING GIN (id app.int_ops (mode = 'off')) WITH (fastupdate = 'off')", statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Fact]
    public void ExpandedIndexes_PreservesSourcesCommentsAndStatementBoundaries()
    {
        const string sql = "/* lead */ CREATE INDEX CONCURRENTLY \"Index\" ON ONLY \"App\".\"Items\" (\"Label\" \"App\".\"TextOps\" (\"Bits\" = 4) NULLS FIRST) TABLESPACE \"Fast\" WHERE \"Label\" IS NOT NULL /* tail */; SELECT 1";
        var statements = ParseStatements(sql, sourceId: 211);
        var statement = Assert.IsType<CreateIndex.Standard>(statements[0]);
        var key = Assert.IsType<KeyPart.Column>(Assert.Single(statement.Columns));

        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.Equal(211, statement.Name!.Source!.Value.SourceId);
        Assert.All(statement.TableName.Values, identifier => Assert.Equal(211, identifier.Source!.Value.SourceId));
        Assert.Equal(211, key.Name.Source!.Value.SourceId);
        Assert.All(key.PgOptions!.OperatorClass!.Values, identifier => Assert.Equal(211, identifier.Source!.Value.SourceId));
        Assert.Equal(211, statement.PgOptions!.Tablespace!.Source!.Value.SourceId);
        Assert.Contains(statement.Meta.PreNonSql!, trivia => trivia is NonSql.BlockComment { Body: " lead " });
        Assert.Contains(statement.Meta.PostNonSql!, trivia => trivia is NonSql.BlockComment { Body: " tail " });
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpandedIndexes_ReportsTargetAndExpressionFunctionsWithoutCapturingCallerScope(bool unique)
    {
        string sql = $"CREATE{(unique ? " UNIQUE" : "")} INDEX items_idx ON items USING bloom (id, app.compute(id) COLLATE app.casefold app.int_ops (bits = 4)) INCLUDE (payload) WITH (custom_parameter = 1) TABLESPACE fast WHERE app.keep(id)";
        var statement = Assert.IsType<CreateIndex>(ParseStatement(sql), exactMatch: false);
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var tables = new PseudoTableSet(schema,
        [new PseudoTable("items", new ObjectIdentifier("items", schema), ["id", "payload"], PseudoTableType.Table)]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        context.AddLocalVariable(new Identifier("items"));
        context.AddLocalVariable(new Identifier("id"));
        using var scope = context.Enter(ReferencedItemsContext.WhereClause);

        ItemRef[] references = [.. statement.GetReferencedItems(context)];

        Assert.Equal(["items", "app.compute", "app.keep"], references.OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql()));
        Assert.Equal(ItemType.Table, references[0].Type);
        Assert.All(references.Skip(1), reference => Assert.Equal(ItemType.Unknown, reference.Type));
        Assert.All(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }), reference => Assert.Equal(ItemType.Unknown, reference.Type));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Single(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Table, PseudoTables = tables }));
        _ = statement.GetReferencedItems(context).Take(1).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        _ = statement.GetReferencedItems(context).Take(2).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        Assert.Same(tables, context.PseudoTables);
    }

    [Fact]
    public void ExpandedIndexes_RoundTripsAtomicBodyWithoutValidatingExecutability()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC CREATE INDEX CONCURRENTLY ON items (id NULLS LAST) WITH (fillfactor = 80) WHERE id > 0; SELECT 1; END; SELECT 2";
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
    [InlineData("CREATE UNIQUE INDEX items_idx ON items USING HASH (id) INCLUDE (payload)")]
    [InlineData("CREATE INDEX items_idx ON items (id) NULLS NOT DISTINCT")]
    [InlineData("CREATE INDEX items_idx ON items (id app.unknown_ops (unknown_parameter = -1)) WITH (fillfactor = 101)")]
    public void ExpandedIndexes_LeavesMethodAndOptionEligibilityToSemanticValidation(string sql)
    {
        var statement = Assert.IsType<CreateIndex>(ParseStatement(sql), exactMatch: false);

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("CREATE INDEX IF NOT EXISTS ON items (id)")]
    [InlineData("CREATE INDEX CONCURRENTLY CONCURRENTLY items_idx ON items (id)")]
    [InlineData("CREATE INDEX items_idx CONCURRENTLY ON items (id)")]
    [InlineData("CREATE INDEX app.items_idx ON items (id)")]
    [InlineData("CREATE INDEX 'items_idx' ON items (id)")]
    [InlineData("CREATE INDEX items_idx ON items ()")]
    [InlineData("CREATE INDEX items_idx ON items (id,)")]
    [InlineData("CREATE INDEX items_idx ON items (id + 1)")]
    [InlineData("CREATE INDEX items_idx ON items (id::INT)")]
    [InlineData("CREATE INDEX items_idx ON items (id COLLATE app.first COLLATE app.second)")]
    [InlineData("CREATE INDEX items_idx ON items (id ASC app.int_ops)")]
    [InlineData("CREATE INDEX items_idx ON items (id NULLS FIRST DESC)")]
    [InlineData("CREATE INDEX items_idx ON items (id NULLS)")]
    [InlineData("CREATE INDEX items_idx ON items (id NULLS DISTINCT)")]
    [InlineData("CREATE INDEX items_idx ON items (id app.int_ops ())")]
    [InlineData("CREATE INDEX items_idx ON items (id app.int_ops (bits))")]
    [InlineData("CREATE INDEX items_idx ON items (id app.int_ops (app.bits = 4))")]
    [InlineData("CREATE INDEX items_idx ON items (id app.int_ops (bits = 1 + 2))")]
    [InlineData("CREATE INDEX items_idx ON items (app.compute(id) OVER ())")]
    [InlineData("CREATE INDEX items_idx USING BTREE ON items (id)")]
    [InlineData("CREATE INDEX items_idx ON items (id) USING BTREE")]
    [InlineData("CREATE INDEX items_idx ON items USING app.bloom (id)")]
    [InlineData("CREATE INDEX items_idx ON items USING BTREE USING HASH (id)")]
    [InlineData("CREATE INDEX items_idx ON items (id) INCLUDE ()")]
    [InlineData("CREATE INDEX items_idx ON items (id) INCLUDE (id + 1)")]
    [InlineData("CREATE INDEX items_idx ON items (id) NULLS FIRST")]
    [InlineData("CREATE INDEX items_idx ON items (id) NULLS NOT")]
    [InlineData("CREATE INDEX items_idx ON items (id) WITH ()")]
    [InlineData("CREATE INDEX items_idx ON items (id) WITH (app.fillfactor. = 80)")]
    [InlineData("CREATE INDEX items_idx ON items (id) WITH (app.toast.fillfactor = 80)")]
    [InlineData("CREATE INDEX items_idx ON items (id) WITH (fillfactor = 1 + 2)")]
    [InlineData("CREATE INDEX items_idx ON items (id) WITH (fillfactor = 80) INCLUDE (label)")]
    [InlineData("CREATE INDEX items_idx ON items (id) TABLESPACE app.fast")]
    [InlineData("CREATE INDEX items_idx ON items (id) TABLESPACE fast WITH (fillfactor = 80)")]
    [InlineData("CREATE INDEX items_idx ON items (id) WHERE id > 0 WITH (fillfactor = 80)")]
    [InlineData("CREATE INDEX items_idx ON items (id) WHERE id > 0 TABLESPACE fast")]
    [InlineData("CREATE INDEX items_idx ON items (id) WITH (fillfactor = 80) WITH (fillfactor = 70)")]
    [InlineData("CREATE INDEX items_idx ON items (id) COMMENT 'index'")]
    [InlineData("CREATE INDEX items_idx ON items (id) ON filegroup_name")]
    [InlineData("CREATE CLUSTERED INDEX items_idx ON items (id)")]
    [InlineData("CREATE FULLTEXT INDEX items_idx ON items (id)")]
    [InlineData("CREATE SPATIAL INDEX items_idx ON items (id)")]
    [InlineData("CREATE OR REPLACE INDEX items_idx ON items (id)")]
    public void ExpandedIndexes_RejectsMalformedAndOtherDialectForms(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
