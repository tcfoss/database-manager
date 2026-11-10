using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgCreateDispatchTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("CREATE FUNCTION f(x INT) RETURNS INT RETURN x", typeof(CreateFunction))]
    [InlineData("CREATE OR REPLACE FUNCTION f() RETURNS INT RETURN 1", typeof(CreateFunction))]
    [InlineData("CREATE PROCEDURE p() BEGIN ATOMIC SELECT 1; END", typeof(CreateProcedure))]
    [InlineData("CREATE VIEW v AS SELECT 1", typeof(CreateView))]
    [InlineData("CREATE OR REPLACE VIEW v AS SELECT 1", typeof(CreateView))]
    [InlineData("CREATE MATERIALIZED VIEW summary AS SELECT 1", typeof(CreateMaterializedView))]
    [InlineData("CREATE MATERIALIZED VIEW IF NOT EXISTS summary (id) AS VALUES (1) WITH NO DATA", typeof(CreateMaterializedView))]
    [InlineData("CREATE MATERIALIZED VIEW summary USING heap WITH (fillfactor = 70, autovacuum_enabled, toast.autovacuum_enabled = false) TABLESPACE fast AS SELECT 1 WITH DATA", typeof(CreateMaterializedView))]
    [InlineData("CREATE INDEX ix ON t (id)", typeof(CreateIndex.Standard))]
    [InlineData("CREATE TEMP TABLE t (id INT)", typeof(CreateTable))]
    [InlineData("CREATE DOMAIN positive AS INTEGER CHECK (VALUE > 0)", typeof(CreateDomain))]
    public void SupportedSharedCreateStatements_Dispatch(string sql, Type expectedType)
    {
        Assert.IsType(expectedType, ParseStatement(sql));
    }

    [Theory]
    [InlineData("CREATE")]
    [InlineData("CREATE *")]
    [InlineData("CREATE unsupported_object")]
    public void InvalidCreateTargets_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE OR REPLACE TABLE t (id INT)")]
    [InlineData("CREATE TEMP INDEX ix ON t (id)")]
    [InlineData("CREATE OR REPLACE DOMAIN d AS INTEGER")]
    [InlineData("CREATE TEMP DOMAIN d AS INTEGER")]
    public void UnsupportedCreateOptions_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE DOMAIN positive AS INT DEFAULT 1 NOT NULL CHECK (VALUE > 0)")]
    [InlineData("CREATE DOMAIN label TEXT COLLATE \"C\" DEFAULT 'it''s ok' NULL")]
    [InlineData("CREATE DOMAIN \"App\".\"Label\" AS \"App\".\"BaseType\" CONSTRAINT \"Valid\" CHECK (VALUE <> '')")]
    [InlineData("CREATE DOMAIN bounded AS NUMERIC(10,-2) CHECK (VALUE BETWEEN 0 AND 100)")]
    public void Domains_RoundTripWithPostgresTypesAndIdentifiers(string sql)
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement(sql));

        Assert.Equal(sql, domain.ToSql());
        Assert.Equal(sql, ParseStatement(domain.ToSql()).ToSql());
    }

    [Fact]
    public void DomainBaseType_UsesPostgresTypeParser()
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement("CREATE DOMAIN created AS TIMESTAMPTZ"));

        Assert.IsType<PgDataType.PgTimestamp>(Assert.IsType<DataTypeReference.BuiltIn>(domain.DataType).DataType);
        Assert.Equal("CREATE DOMAIN created AS TIMESTAMP WITH TIME ZONE", domain.ToSql());
    }

    [Theory]
    [InlineData("CREATE DOMAIN 'label' AS TEXT")]
    [InlineData("CREATE DOMAIN [label] AS TEXT")]
    [InlineData("CREATE DOMAIN label AS VARCHAR(0)")]
    [InlineData("CREATE DOMAIN label AS app.")]
    [InlineData("CREATE DOMAIN label AS TEXT COLLATE 'C'")]
    [InlineData("CREATE DOMAIN label AS TEXT CONSTRAINT 'valid' CHECK (VALUE <> '')")]
    [InlineData("CREATE DOMAIN label AS TEXT DEFAULT 'x' DEFAULT 'y'")]
    public void InvalidDomains_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON app.items EXECUTE FUNCTION app.audit()")]
    [InlineData("CREATE OR REPLACE TRIGGER audit BEFORE UPDATE OF id, name OR DELETE ON app.items FOR EACH ROW EXECUTE PROCEDURE app.audit()")]
    [InlineData("CREATE TRIGGER audit INSTEAD OF INSERT OR UPDATE OR DELETE ON items FOR ROW EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER TRUNCATE ON items FOR EACH STATEMENT EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items REFERENCING NEW TABLE AS new_rows FOR STATEMENT EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER DELETE ON items REFERENCING OLD TABLE old_rows EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER UPDATE ON items REFERENCING NEW TABLE new_rows OLD TABLE AS old_rows FOR EACH STATEMENT EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit BEFORE UPDATE ON items FOR ROW WHEN (NEW.id > OLD.id) EXECUTE FUNCTION audit('it''s ok', 1, 1.5, named, \"Quoted\", $$text$$)")]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER INSERT OR UPDATE ON items FOR EACH ROW EXECUTE FUNCTION audit()")]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER UPDATE ON items FROM app.other DEFERRABLE INITIALLY DEFERRED FOR ROW WHEN (NEW.id > 0) EXECUTE FUNCTION app.audit()")]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER DELETE ON items NOT DEFERRABLE INITIALLY IMMEDIATE FOR EACH ROW EXECUTE PROCEDURE audit()")]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER UPDATE ON items INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION audit()")]
    public void Triggers_RoundTrip(string sql)
    {
        var trigger = Assert.IsType<CreateTrigger>(ParseStatement(sql));

        Assert.Equal(sql, trigger.ToSql());
        Assert.Equal(trigger, ParseStatement(trigger.ToSql()));
        Assert.IsType<PgTriggerExecute>(trigger.Body);
    }

    [Theory]
    [InlineData("CREATE TRIGGER audit AFTER UPDATE OF ON items EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT, UPDATE ON items EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER app.audit AFTER INSERT ON items EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT OR ON items EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit FOR INSERT ON items EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit ON items AFTER INSERT EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER IF NOT EXISTS audit AFTER INSERT ON items EXECUTE FUNCTION audit()")]
    [InlineData("CREATE OR REPLACE CONSTRAINT TRIGGER audit AFTER INSERT ON items FOR EACH ROW EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items FOR EACH EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items FOR UNKNOWN EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items AS EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items SELECT 1")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items FOLLOWS other EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items FROM other EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items DEFERRABLE EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items REFERENCING EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items REFERENCING OLD ROW old_rows EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items REFERENCING OLD TABLE AS EXECUTE FUNCTION audit()")]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER UPDATE ON items INITIALLY UNKNOWN FOR EACH ROW EXECUTE FUNCTION audit()")]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER UPDATE ON items DEFERRABLE DEFERRABLE FOR EACH ROW EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items WHEN NEW.id > 0 EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items WHEN () EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items EXECUTE audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items body_label: EXECUTE FUNCTION audit()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION audit")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION audit(1 + 2)")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION audit(named.other)")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION audit('x',)")]
    public void InvalidTriggers_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void TriggerExecute_PreservesBodyMetadata()
    {
        const string sql = "CREATE TRIGGER audit AFTER INSERT ON items /* before */ EXECUTE FUNCTION audit() /* after */;";
        var trigger = Assert.IsType<CreateTrigger>(ParseStatement(sql));
        var execute = Assert.IsType<PgTriggerExecute>(trigger.Body);

        Assert.Equal(sql.IndexOf(" /* before */", StringComparison.Ordinal), execute.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), execute.Meta.End);
        Assert.Equal(sql.Length, execute.Meta.EndPlusTerminatorLength);
        Assert.NotNull(execute.Meta.PreNonSql);
        Assert.NotEmpty(execute.Meta.PreNonSql);
        Assert.NotNull(execute.Meta.PostNonSql);
        Assert.NotEmpty(execute.Meta.PostNonSql);
    }

    [Fact]
    public void ConstraintTrigger_PreservesParsedComponentsAndSerializes()
    {
        const string sql = "CREATE CONSTRAINT TRIGGER audit AFTER UPDATE OF id, name OR INSERT ON app.items FROM app.parent NOT DEFERRABLE INITIALLY IMMEDIATE FOR ROW WHEN (NEW.id > 0) EXECUTE PROCEDURE app.audit(named, \"Quoted\", 'it''s ok', 1.5)";
        var trigger = Assert.IsType<CreateTrigger>(ParseStatement(sql));
        Assert.NotNull(trigger.PgOptions);
        Assert.True(trigger.PgOptions.EventsUseOr);
        var update = Assert.IsType<TriggerEvent.Update>(trigger.Events[0]);
        Assert.NotNull(update.Columns);
        Assert.Equal(["id", "name"], update.Columns.Select(column => column.Name));
        Assert.IsType<TriggerEvent.Insert>(trigger.Events[1]);
        var constraint = Assert.IsType<PgTriggerConstraint>(trigger.PgOptions.ConstraintOptions);
        Assert.Equal("app.parent", constraint.FromTable?.ToSql());
        Assert.False(constraint.Deferrable);
        Assert.False(constraint.InitiallyDeferred);
        Assert.Null(trigger.PgOptions.TransitionRelations);
        Assert.NotNull(trigger.PgOptions.When);
        var quantifier = Assert.IsType<TriggerExecutionQuantifier.ForEach>(trigger.ExecutionQuantifier);
        Assert.Equal(TriggerForEachType.Row, quantifier.Type);
        Assert.False(quantifier.UseEach);
        var execute = Assert.IsType<PgTriggerExecute>(trigger.Body);
        Assert.True(execute.UseProcedure);
        Assert.Equal("app.audit", execute.FunctionName.ToSql());
        Assert.Equal("named", Assert.IsType<PgStatementTriggerArgument.Named>(execute.Arguments[0]).Name.Name);
        Assert.Equal(QuoteStyle.Ansi, Assert.IsType<PgStatementTriggerArgument.Named>(execute.Arguments[1]).Name.QuoteStyle);
        Assert.IsType<Value.SingleQuotedString>(Assert.IsType<PgStatementTriggerArgument.Literal>(execute.Arguments[2]).Value);
        Assert.IsType<Value.Number>(Assert.IsType<PgStatementTriggerArgument.Literal>(execute.Arguments[3]).Value);
        Assert.Equal(trigger, Serialization.FromJson<CreateTrigger>(Serialization.ToJson(trigger)));
    }

    [Fact]
    public void TriggerTransitionRelations_PreserveOrderAndOptionalAs()
    {
        var trigger = Assert.IsType<CreateTrigger>(ParseStatement("CREATE TRIGGER audit AFTER UPDATE ON items REFERENCING NEW TABLE new_rows OLD TABLE AS old_rows FOR STATEMENT EXECUTE FUNCTION audit()"));
        Assert.NotNull(trigger.PgOptions);
        Assert.Null(trigger.PgOptions.ConstraintOptions);
        Assert.NotNull(trigger.PgOptions.TransitionRelations);
        Assert.Collection(trigger.PgOptions.TransitionRelations,
            relation => Assert.Equal(new PgTriggerTransitionRelation(true, new Identifier("new_rows"), false), relation),
            relation => Assert.Equal(new PgTriggerTransitionRelation(false, new Identifier("old_rows"), true), relation));
        var quantifier = Assert.IsType<TriggerExecutionQuantifier.ForEach>(trigger.ExecutionQuantifier);
        Assert.Equal(TriggerForEachType.Statement, quantifier.Type);
        Assert.False(quantifier.UseEach);
    }

    [Theory]
    [InlineData("COMMENT ON TABLE app.items IS 'it''s a table'", "it''s a table")]
    [InlineData("COMMENT ON COLUMN app.items.id IS ''", "")]
    [InlineData("COMMENT ON TABLE app.items IS NULL", null)]
    [InlineData("COMMENT ON FUNCTION app.f(IN value INT, OUT result TEXT) IS 'function'", "function")]
    [InlineData("COMMENT ON FUNCTION app.f() IS 'empty signature'", "empty signature")]
    [InlineData("COMMENT ON PROCEDURE app.p IS 'procedure'", "procedure")]
    [InlineData("COMMENT ON TRIGGER audit ON app.items IS 'trigger'", "trigger")]
    [InlineData("COMMENT ON INDEX app.ix IS 'index'", "index")]
    [InlineData("COMMENT ON TYPE app.kind IS 'type'", "type")]
    [InlineData("COMMENT ON DOMAIN app.label IS 'domain'", "domain")]
    [InlineData("COMMENT ON VIEW app.v IS 'view'", "view")]
    [InlineData("COMMENT ON SEQUENCE app.seq IS 'sequence'", "sequence")]
    [InlineData("COMMENT ON TABLE app.items IS 'it''''s'", "it''''s")]
    [InlineData("COMMENT ON TABLE app.items IS 'x''; DROP TABLE items; --'", "x''; DROP TABLE items; --")]
    public void Comments_RoundTrip(string sql, string? expected)
    {
        var comment = Assert.IsType<CommentOn>(ParseStatement(sql));

        if (expected == null)
        {
            Assert.IsType<Value.Null>(comment.Comment);
        }
        else
        {
            Assert.Equal(expected, Assert.IsType<Value.SingleQuotedString>(comment.Comment).Value);
        }
        Assert.Equal(sql, comment.ToSql());
        Assert.Equal(comment, ParseStatement(comment.ToSql()));
        Assert.Equal(comment, Serialization.FromJson<CommentOn>(Serialization.ToJson(comment)));
    }

    [Fact]
    public void Comments_DollarQuotedTextPreservesTagAndApostrophes()
    {
        var comment = Assert.IsType<CommentOn>(ParseStatement("COMMENT ON TABLE items IS $tag$it''s; opaque$tag$"));

        var literal = Assert.IsType<Value.DollarQuotedString>(comment.Comment);
        Assert.Equal("it''s; opaque", literal.Value);
        Assert.Equal("tag", literal.Tag);
        Assert.Equal("COMMENT ON TABLE items IS $tag$it''s; opaque$tag$", comment.ToSql());
        Assert.Equal(comment, ParseStatement(comment.ToSql()));
        Assert.Equal("tag", Assert.IsType<Value.DollarQuotedString>(Serialization.FromJson<CommentOn>(Serialization.ToJson(comment))!.Comment).Tag);
    }

    [Theory]
    [InlineData("locale")]
    [InlineData("app.locale")]
    [InlineData("\"C\"")]
    [InlineData("\"App\".\"Locale\"")]
    public void ColumnCollations_PreserveNamesAndRoundTrip(string collation)
    {
        string sql = $"CREATE TABLE items (label TEXT COLLATE {collation} NOT NULL)";

        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));

        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(statement.Columns));
        var option = Assert.IsType<StatementColumnOption.PgCollation>(column.Options[0]);
        Assert.Equal(collation, option.Name.ToSql());
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Assert.Equal(statement, Serialization.FromJson<CreateTable>(Serialization.ToJson(statement)));
    }

    [Fact]
    public void ColumnCollations_EscapeEmbeddedIdentifierQuotesInSql()
    {
        const string sql = "CREATE TABLE items (label TEXT COLLATE \"Locale\"\"Name\")";

        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("COLLATE")]
    [InlineData("COLLATE 'C'")]
    [InlineData("COLLATE 1")]
    [InlineData("COLLATE = locale")]
    [InlineData("COLLATE app.")]
    [InlineData("COLLATE \"C\" COLLATE \"POSIX\"")]
    [InlineData("CONSTRAINT named COLLATE \"C\"")]
    public void ColumnCollations_RejectInvalidDeclarations(string clause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"CREATE TABLE items (label TEXT {clause})"));
    }

    [Theory]
    [InlineData("CREATE MATERIALIZED VIEW app.summary AS SELECT id FROM app.items")]
    [InlineData("CREATE MATERIALIZED VIEW IF NOT EXISTS summary (id) AS VALUES (1), (2) WITH NO DATA")]
    [InlineData("CREATE MATERIALIZED VIEW summary USING heap AS SELECT 1 WITH DATA")]
    [InlineData("CREATE MATERIALIZED VIEW summary WITH (fillfactor = 70, autovacuum_enabled) AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary WITH (toast.autovacuum_enabled = FALSE) TABLESPACE fast AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary AS WITH seed (id) AS (SELECT 1) SELECT id FROM seed WITH DATA")]
    [InlineData("CREATE MATERIALIZED VIEW summary AS (SELECT 1 UNION SELECT 2) WITH NO DATA")]
    [InlineData("CREATE MATERIALIZED VIEW summary WITH (fillfactor = +70, autovacuum_vacuum_cost_delay = -1) AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW \"App\".\"Summary\" (\"Id\") USING \"Heap\" TABLESPACE \"Fast\" AS SELECT 1 WITH NO DATA")]
    public void MaterializedViews_RoundTripSqlAndJson(string sql)
    {
        var statement = Assert.IsType<CreateMaterializedView>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Assert.Equal(statement, Serialization.FromJson<CreateMaterializedView>(Serialization.ToJson(statement)));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(" WITH DATA", true)]
    [InlineData(" WITH NO DATA", false)]
    public void MaterializedViews_PreservePopulationClausePresence(string clause, bool? expected)
    {
        var statement = Assert.IsType<CreateMaterializedView>(ParseStatement($"CREATE MATERIALIZED VIEW summary AS SELECT 1{clause}"));

        Assert.Equal(expected, statement.WithData);
        Assert.Null(statement.Columns);
        Assert.Null(statement.AccessMethod);
        Assert.Null(statement.StorageParameters);
        Assert.Null(statement.Tablespace);
        Assert.False(statement.IfNotExists);
    }

    [Fact]
    public void MaterializedViews_PreserveDeclarationOptions()
    {
        const string sql = "CREATE MATERIALIZED VIEW IF NOT EXISTS app.summary (id, label) USING heap WITH (fillfactor = 70, autovacuum_enabled, toast.autovacuum_enabled = FALSE) TABLESPACE fast AS SELECT id, label FROM app.items WITH NO DATA";

        var statement = Assert.IsType<CreateMaterializedView>(ParseStatement(sql));

        Assert.Equal("app.summary", statement.Name.ToSql());
        Assert.Equal(new[] { "id", "label" }, statement.Columns!.Select(column => column.Name));
        Assert.True(statement.IfNotExists);
        Assert.Equal("heap", statement.AccessMethod?.Name);
        Assert.Equal("fast", statement.Tablespace?.Name);
        Assert.False(statement.WithData);
        Assert.Collection(statement.StorageParameters!,
            parameter => Assert.Equal("70", Assert.IsType<Value.Number>(parameter.Value).Value),
            parameter => Assert.Null(parameter.Value),
            parameter =>
            {
                Assert.Equal("toast.autovacuum_enabled", parameter.Name.ToSql());
                Assert.False(Assert.IsType<Value.Boolean>(parameter.Value).Value);
            });
    }

    [Theory]
    [InlineData("on", "'on'")]
    [InlineData("off", "'off'")]
    [InlineData("'it''s; opaque'", "'it''s; opaque'")]
    [InlineData("$$opaque; text$$", "$$opaque; text$$")]
    public void MaterializedViews_PreserveScalarStorageValues(string input, string expected)
    {
        var statement = Assert.IsType<CreateMaterializedView>(ParseStatement($"CREATE MATERIALIZED VIEW summary WITH (setting = {input}) AS SELECT 1"));

        Assert.Equal(expected, Assert.Single(statement.StorageParameters!).Value!.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Fact]
    public void MaterializedViews_PreserveStatementBoundariesAndQueryDependencies()
    {
        const string sql = "CREATE MATERIALIZED VIEW app.summary USING heap WITH (fillfactor = 70) TABLESPACE fast AS SELECT app.adjust(id) AS id FROM app.items WITH NO DATA; SELECT 2;";
        var statements = new PgParser().Parse(GetState(sql, sourceId: 137), null);
        var statement = Assert.IsType<CreateMaterializedView>(statements[0]);

        Assert.Equal(2, statements.Count);
        Assert.Equal(0, statement.Meta.Start);
        Assert.Equal(sql.IndexOf(';'), statement.Meta.End);
        Assert.Equal(sql.IndexOf(';') + 1, statement.Meta.EndPlusTerminatorLength);
        Assert.Equal(137, statement.Name.Values[1].Source?.SourceId);
        Assert.EndsWith("FROM app.items", statement.Body.ToSql());
        Assert.Equal("SELECT 2", statements[1].ToSql());
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, FunctionNameProvider = new FunctionNameProvider() };
        string[] dependencies = [.. statement.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql()).OrderBy(name => name, StringComparer.Ordinal)];
        Assert.Equal(new[] { "app.adjust", "app.items" }, dependencies);
    }

    [Theory]
    [InlineData("CREATE OR REPLACE MATERIALIZED VIEW summary AS SELECT 1")]
    [InlineData("CREATE TEMP MATERIALIZED VIEW summary AS SELECT 1")]
    [InlineData("CREATE TEMPORARY MATERIALIZED VIEW summary AS SELECT 1")]
    [InlineData("CREATE UNLOGGED MATERIALIZED VIEW summary AS SELECT 1")]
    [InlineData("CREATE RECURSIVE MATERIALIZED VIEW summary AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED summary AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary () AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary USING AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary WITH () AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary WITH (fillfactor =) AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary WITH (fillfactor = 1 + 2) AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary WITH (fillfactor = 70,) AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary WITH (app.toast.setting = 1) AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary TABLESPACE app.fast AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary TABLESPACE fast USING heap AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary AS INSERT INTO items VALUES (1)")]
    [InlineData("CREATE MATERIALIZED VIEW summary AS WITH seed AS (SELECT 1) INSERT INTO items VALUES (1)")]
    [InlineData("CREATE MATERIALIZED VIEW summary AS SELECT 1 WITH CHECK OPTION")]
    [InlineData("CREATE MATERIALIZED VIEW summary AS SELECT 1 WITH NO")]
    [InlineData("CREATE MATERIALIZED VIEW summary AS SELECT 1 WITH DATA WITH DATA")]
    public void MaterializedViews_RejectInvalidDeclarations(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
