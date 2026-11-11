using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
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

public class PgConstraintParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("CREATE TABLE items (id INT, CONSTRAINT uq UNIQUE NULLS NOT DISTINCT (id) DEFERRABLE INITIALLY DEFERRED)")]
    [InlineData("CREATE TABLE items (id INT CONSTRAINT pk PRIMARY KEY NOT DEFERRABLE INITIALLY IMMEDIATE)")]
    [InlineData("CREATE TABLE items (id INT UNIQUE NULLS DISTINCT DEFERRABLE)")]
    [InlineData("CREATE TABLE items (id INT CHECK (id > 0) NO INHERIT)")]
    [InlineData("CREATE TABLE items (id INT REFERENCES app.parents (id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED)")]
    [InlineData("CREATE TABLE items (id INT REFERENCES app.parents)")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE USING GIST (id WITH =))")]
    [InlineData("CREATE TABLE items (id INT, CONSTRAINT ex EXCLUDE ((id + 1) WITH &&) WHERE (id > 0) DEFERRABLE)")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE USING \"custom\" (id app.int_ops (bits = 4) DESC NULLS LAST WITH OPERATOR(app.&&)))")]
    [InlineData("ALTER TABLE items ADD CONSTRAINT fk FOREIGN KEY (id) REFERENCES app.parents (id) DEFERRABLE INITIALLY DEFERRED NOT VALID")]
    [InlineData("ALTER TABLE items ADD CONSTRAINT ck CHECK (id > 0) NO INHERIT NOT VALID")]
    [InlineData("ALTER TABLE items ADD EXCLUDE USING GIST (id WITH =)")]
    [InlineData("ALTER TABLE items ADD CONSTRAINT pk PRIMARY KEY USING INDEX items_idx DEFERRABLE")]
    [InlineData("ALTER TABLE items ADD UNIQUE USING INDEX items_idx")]
    [InlineData("ALTER TABLE IF EXISTS ONLY app.items DROP CONSTRAINT IF EXISTS uq CASCADE")]
    [InlineData("ALTER TABLE items DROP CONSTRAINT uq RESTRICT")]
    [InlineData("ALTER TABLE ONLY items RENAME CONSTRAINT old_name TO new_name")]
    [InlineData("ALTER TABLE items ALTER CONSTRAINT fk DEFERRABLE INITIALLY DEFERRED")]
    [InlineData("ALTER TABLE items ALTER CONSTRAINT fk INITIALLY IMMEDIATE")]
    [InlineData("ALTER TABLE items VALIDATE CONSTRAINT fk")]
    [InlineData("ALTER TABLE items ADD CONSTRAINT ck CHECK (id > 0) NOT VALID, VALIDATE CONSTRAINT ck, DROP CONSTRAINT ck")]
    [InlineData("CREATE TABLE items (id INT, label TEXT, PRIMARY KEY (id) INCLUDE (label) USING INDEX TABLESPACE fast)")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE NULLS DISTINCT (id) WITH (deduplicate_items = 'off') USING INDEX TABLESPACE fast INITIALLY DEFERRED)")]
    [InlineData("CREATE TABLE items (id INT UNIQUE WITH (fillfactor = 70) DEFERRABLE)")]
    [InlineData("CREATE TABLE items (id INT, label TEXT, EXCLUDE USING GIST (id WITH =, label COLLATE app.casefold app.text_ops ASC NULLS FIRST WITH OPERATOR(app.&&)) INCLUDE (label) WITH (fillfactor = 70) USING INDEX TABLESPACE fast WHERE (app.keep(id)) DEFERRABLE INITIALLY IMMEDIATE)")]
    [InlineData("CREATE TABLE \"Table; DROP TABLE items\" (\"Column\" INT, CONSTRAINT \"Constraint; SELECT 1\" EXCLUDE USING \"Method\" (\"Column\" \"App\".\"Ops\" (\"Bits\" = 4) WITH OPERATOR(\"App\".&&)) USING INDEX TABLESPACE \"Space\")")]
    [InlineData("ALTER TABLE \"Table\" RENAME CONSTRAINT \"Old\" TO \"New\"")]
    [InlineData("ALTER TABLE items ALTER CONSTRAINT fk NOT DEFERRABLE INITIALLY IMMEDIATE")]
    public void Constraints_RoundTripsSqlJsonAndFormatting(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);

        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
        Assert.IsType(statement.GetType(), ParseStatement(formatted));
    }

    [Fact]
    public void Constraints_PreservesWrittenOptionsWithoutInventingDefaults()
    {
        var plain = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT UNIQUE, PRIMARY KEY (id))"));
        Assert.Null(Assert.IsType<StatementColumnOption.Unique>(Assert.Single(Assert.Single(plain.Columns).Options)).PgOptions);
        Assert.Null(Assert.Single(plain.Constraints).PgOptions);

        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT, CONSTRAINT uq UNIQUE NULLS NOT DISTINCT (id) NOT DEFERRABLE INITIALLY IMMEDIATE)"));
        var unique = Assert.IsType<StatementTableConstraint.UniqueConstraint>(Assert.Single(statement.Constraints));
        Assert.False(unique.NullsDistinct);
        Assert.Collection(unique.PgOptions!,
            option => Assert.False(Assert.IsType<PgConstraintOption.Deferrable>(option).Value),
            option => Assert.False(Assert.IsType<PgConstraintOption.InitiallyDeferred>(option).Value));

        var alter = Assert.IsType<AlterTable>(ParseStatement("ALTER TABLE items ADD CONSTRAINT fk FOREIGN KEY (id) REFERENCES app.parents (id) INITIALLY DEFERRED NOT VALID"));
        var foreignKey = Assert.IsType<AlterTableOperation.AddForeignKey>(Assert.Single(alter.Operations)).ForeignKey;
        Assert.Collection(foreignKey.PgOptions!,
            option => Assert.True(Assert.IsType<PgConstraintOption.InitiallyDeferred>(option).Value),
            option => Assert.IsType<PgConstraintOption.NotValid>(option));
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT, UNIQUE (id) INITIALLY DEFERRED DEFERRABLE)", "INITIALLY DEFERRED DEFERRABLE")]
    [InlineData("CREATE TABLE items (id INT UNIQUE INITIALLY IMMEDIATE NOT DEFERRABLE)", "INITIALLY IMMEDIATE NOT DEFERRABLE")]
    [InlineData("ALTER TABLE items ALTER CONSTRAINT fk INITIALLY DEFERRED DEFERRABLE", "INITIALLY DEFERRED DEFERRABLE")]
    [InlineData("ALTER TABLE items ADD CONSTRAINT fk FOREIGN KEY (id) REFERENCES app.parents (id) NOT VALID INITIALLY DEFERRED DEFERRABLE", "NOT VALID INITIALLY DEFERRED DEFERRABLE")]
    public void Constraints_PreservesWrittenAttributeOrder(string sql, string attributes)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Contains(attributes, formatted);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("WITH (fillfactor = 70)")]
    [InlineData("USING INDEX TABLESPACE fast")]
    [InlineData("WITH (fillfactor = 70) USING INDEX TABLESPACE fast")]
    public void Constraints_IndexParametersPrintAsACompleteComponent(string parameters)
    {
        string sql = $"CREATE TABLE items (id INT, EXCLUDE (id WITH =) {parameters})";
        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));
        var exclusion = Assert.IsType<StatementTableConstraint.PgExclusion>(Assert.Single(statement.Constraints));

        Assert.Null(exclusion.PgOptions);
        Assert.Equal(parameters, exclusion.IndexParameters!.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void Constraints_SeparatesIndexParametersNullDistinctnessAndAttributes()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT UNIQUE NULLS DISTINCT USING INDEX TABLESPACE inline_space, CONSTRAINT uq UNIQUE NULLS NOT DISTINCT (id) WITH (deduplicate_items = 'off') USING INDEX TABLESPACE table_space)"));
        var inline = Assert.IsType<StatementColumnOption.Unique>(Assert.Single(Assert.Single(statement.Columns).Options));
        var unique = Assert.IsType<StatementTableConstraint.UniqueConstraint>(Assert.Single(statement.Constraints));

        Assert.True(inline.NullsDistinct);
        Assert.Null(inline.PgOptions);
        Assert.Null(inline.IndexParameters!.StorageParameters);
        Assert.Equal("inline_space", inline.IndexParameters.Tablespace!.Name);
        Assert.False(unique.NullsDistinct);
        Assert.Null(unique.PgOptions);
        Assert.Single(unique.IndexParameters!.StorageParameters!);
        Assert.Equal("table_space", unique.IndexParameters.Tablespace!.Name);
    }

    [Fact]
    public void Constraints_PreservesExclusionPayload()
    {
        const string sql = "CREATE TABLE items (id INT, CONSTRAINT ex EXCLUDE USING \"custom\" (id app.int_ops (bits = 4) DESC NULLS LAST WITH OPERATOR(app.&&)) INCLUDE (id) WITH (fillfactor = 70) USING INDEX TABLESPACE fast WHERE (app.keep(id)) DEFERRABLE)";
        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));
        var exclusion = Assert.IsType<StatementTableConstraint.PgExclusion>(Assert.Single(statement.Constraints));

        Assert.Equal("custom", Assert.IsType<IndexMethod.Custom>(exclusion.Method).Method.Name);
        var element = Assert.Single(exclusion.Elements);
        var key = Assert.IsType<KeyPart.Column>(element.Key);
        Assert.Equal("app.int_ops", key.PgOptions!.OperatorClass!.ToSql());
        Assert.Single(key.PgOptions.OperatorClassParameters!);
        Assert.Equal(NullsOrder.Last, key.PgOptions.NullsOrder);
        Assert.Equal("&&", element.Operator.Name);
        Assert.Equal("app", element.Operator.Schema!.Name);
        Assert.True(element.Operator.UseOperatorSyntax);
        Assert.NotNull(exclusion.IncludedColumns);
        Assert.Single(exclusion.IndexParameters!.StorageParameters!);
        Assert.Equal("fast", exclusion.IndexParameters.Tablespace!.Name);
        Assert.True(Assert.IsType<PgConstraintOption.Deferrable>(Assert.Single(exclusion.PgOptions!)).Value);
        Assert.NotNull(exclusion.Predicate);
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT REFERENCES app.parents (id))", "app.parents")]
    [InlineData("CREATE TABLE items (id INT, FOREIGN KEY (id) REFERENCES app.parents (id))", "app.parents")]
    [InlineData("CREATE TABLE items (id INT CHECK (app.keep(id)))", "app.keep")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE ((app.compute(id)) app.ops WITH OPERATOR(app.&&)) WHERE (app.keep(id)))", "app.compute,app.keep")]
    [InlineData("ALTER TABLE items ADD EXCLUDE ((app.compute(id)) app.ops WITH OPERATOR(app.&&)) WHERE (app.keep(id))", "items,app.compute,app.keep")]
    [InlineData("ALTER TABLE items ADD CONSTRAINT fk FOREIGN KEY (id) REFERENCES app.parents (id) NOT VALID", "items,app.parents")]
    [InlineData("ALTER TABLE items ADD id INT REFERENCES app.parents (id)", "items,app.parents")]
    public void Constraints_ReportsDependenciesWithoutCapturingCallerScope(string sql, string expected)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var tables = new PseudoTableSet(schema,
        [new PseudoTable("items", new ObjectIdentifier("items", schema), ["id"], PseudoTableType.Table)]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        context.AddLocalVariable(new Identifier("id"));
        using var scope = context.Enter(ReferencedItemsContext.WhereClause);

        ItemRef[] references = [.. statement.GetReferencedItems(context)];

        Assert.Equal(expected.Split(','), references.OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql()));
        Assert.All(references, reference => Assert.IsType<ItemRef.ObjectNameRef>(reference));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        _ = statement.GetReferencedItems(context).Take(1).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        _ = statement.GetReferencedItems(context).Take(2).ToArray();
        Assert.Equal(ReferencedItemsContext.WhereClause, context.CurrentContext);
        Assert.Same(tables, context.PseudoTables);
    }

    [Fact]
    public void Constraints_PreservesAtomicAndStatementBoundaries()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC ALTER TABLE items ADD CONSTRAINT ck CHECK (id > 0) NOT VALID; ALTER TABLE items VALIDATE CONSTRAINT ck; END; SELECT 1";
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

    [Fact]
    public void Constraints_PreservesSourcesCommentsAndStatementBoundaries()
    {
        const string sql = "/* lead */ ALTER TABLE \"Items\" ADD CONSTRAINT \"Check\" CHECK (id > 0) NOT VALID /* tail */; SELECT 1";
        var statements = ParseStatements(sql, 7);
        var statement = Assert.IsType<AlterTable>(statements[0]);

        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(7, statement.Table.Source!.Value.SourceId);
        Assert.Equal(sql.IndexOf("\"Items\"", StringComparison.Ordinal), statement.Table.Source.Value.Start);
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Contains("/* lead */", formatted);
        Assert.Contains("/* tail */", formatted);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE USING GIN (id WITH =))")]
    [InlineData("ALTER TABLE items ALTER CONSTRAINT unknown_constraint DEFERRABLE INITIALLY DEFERRED")]
    public void Constraints_LeavesEligibilityAndCatalogChecksToSemanticValidation(string sql)
    {
        Assert.Equal(ParseStatement(sql), ParseStatement(ParseStatement(sql).ToSql()));
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE ())")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (id))")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (id WITH))")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (id WITH equals))")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE USING (id WITH =))")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (id WITH =,))")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (id WITH OPERATOR(app.)))")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (id WITH OPERATOR(app.schema.&&)))")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (app.compute(id) WITH =))")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (id + 1 WITH =))")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (id WITH =) WHERE id > 0)")]
    [InlineData("CREATE TABLE items (id INT, EXCLUDE (id app.ops (bits) WITH =))")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE NULLS (id))")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE NULLS NOT (id))")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE (id) NULLS NOT DISTINCT)")]
    [InlineData("CREATE TABLE items (id INT, PRIMARY KEY (id DESC))")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE ((id + 1)))")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE KEY ix (id))")]
    [InlineData("CREATE TABLE items (id INT, PRIMARY KEY USING BTREE (id))")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE (id) DEFERRABLE NOT DEFERRABLE)")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE (id) INITIALLY DEFERRED INITIALLY IMMEDIATE)")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE (id) INITIALLY)")]
    [InlineData("CREATE TABLE items (id INT, CHECK (id > 0) NOT VALID)")]
    [InlineData("CREATE TABLE items (id INT REFERENCES app.parents ())")]
    [InlineData("ALTER TABLE items ADD CONSTRAINT ck CHECK (id > 0) NOT VALID NOT VALID")]
    [InlineData("ALTER TABLE items ADD CONSTRAINT uq UNIQUE USING INDEX")]
    [InlineData("CREATE TABLE items (id INT, UNIQUE USING INDEX items_idx)")]
    [InlineData("ALTER TABLE items ALTER CONSTRAINT fk")]
    [InlineData("ALTER TABLE items ALTER CONSTRAINT fk NOT VALID")]
    [InlineData("ALTER TABLE items VALIDATE fk")]
    [InlineData("ALTER TABLE items VALIDATE CONSTRAINT")]
    [InlineData("ALTER TABLE items DROP CONSTRAINT IF EXISTS")]
    [InlineData("ALTER TABLE items DROP CONSTRAINT uq CASCADE RESTRICT")]
    [InlineData("ALTER TABLE items RENAME CONSTRAINT uq new_name")]
    [InlineData("ALTER TABLE items RENAME CONSTRAINT uq TO new_name, VALIDATE CONSTRAINT new_name")]
    [InlineData("ALTER TABLE items VALIDATE CONSTRAINT uq, RENAME CONSTRAINT uq TO new_name")]
    public void Constraints_RejectsMalformedAndOtherDialectSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
