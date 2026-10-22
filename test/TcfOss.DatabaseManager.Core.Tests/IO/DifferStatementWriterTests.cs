using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Tests.IO;

public class DifferStatementWriterTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void UseSchemaOnChange_SchemaChanges(bool changeSchema, bool useSchemaOnChange, bool expectUseStatement)
    {
        var (writer, output) = ConstructWriter(useSchemaOnChange: useSchemaOnChange);

        SchemaIdentifier schema = SI();

        var ops = new DefinitionAlterStatement(
            1,
            schema,
            new AlterTable(ON("mytable", "myschema"), [new AlterTableOperation.TableComment("My table comment")]),
            "My operation comment");
        writer.WriteDefinitionAlter(ops);

        if (changeSchema)
        {
            schema = SI("myschema2");
        }

        ops = new DefinitionAlterStatement(
            2,
            schema,
            new AlterTable(ON("mytable", "myschema"), [new AlterTableOperation.TableComment("My table comment")]),
            "My operation comment");
        writer.WriteDefinitionAlter(ops);

        if (expectUseStatement)
        {
            Assert.Contains(output.ToString().Split(Environment.NewLine), x => x.StartsWith("USE \"myschema2"));
        }
        else
        {
            Assert.DoesNotContain(output.ToString().Split(Environment.NewLine), x => x.StartsWith("USE \"myschema2"));
        }
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void EmitComments(bool emitComments, bool hasComment, bool expectComment)
    {
        string? comment = hasComment ? "My operation comment" : null;
        var ops = new DefinitionAlterStatement(
            1,
            SI(),
            new AlterTable(ON("mytable", "myschema"), [new AlterTableOperation.TableComment("My table comment")]),
            comment
        );

        var (writer, output) = ConstructWriter(emitComments: emitComments);
        writer.WriteDefinitionAlter(ops);

        var actual = output.ToString();
        if (expectComment)
        {
            Assert.Contains("/* My operation comment (1) */", actual);
        }
        else
        {
            Assert.DoesNotContain("/* My operation comment (1) */", actual);
        }
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void TerminateStatements(bool terminateStatements, bool autoEndsWithSemicolon, bool expectTerminator)
    {
        var (writer, output) = ConstructWriter(terminateStatements: terminateStatements);

        var operation = new AlterTableOperation.TableComment("My table comment");
        Statement stmt;
        if (autoEndsWithSemicolon)
        {
            stmt = new FakeAlter(ON("mytable", "myschema"), operation);
        }
        else
        {
            stmt = new AlterTable(ON("mytable", "myschema"), [operation]);
        }

        var ops = new DefinitionAlterStatement(
            1,
            SI(),
            stmt,
            "My operation comment"
        );

        writer.WriteDefinitionAlter(ops);

        var actual = output.ToString().TrimEnd();
        if (expectTerminator)
        {
            Assert.EndsWith(";", actual);
        }
        else
        {
            Assert.False(actual.EndsWith(';'), actual);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TerminateStatements_MySqlStyle_Programmatic(bool terminateStatements)
    {
        var (writer, output) = ConstructWriter(terminateStatements: terminateStatements);

        var stmt = new CreateProcedure(ON("myproc"), [], new Select(new SelectBody.SimpleSelectQuery(new SimpleSelect([new SimpleSelectItem.Wildcard()]))));

        writer.WriteStatement(stmt);

        var actual = output.ToString().TrimEnd();

        Assert.Contains("DELIMITER //", actual);

        var actualLines = actual.Split(Environment.NewLine).Where(x => !string.IsNullOrWhiteSpace(x));
        var secondToLast = actualLines.Reverse().Skip(1).First().Trim();

        if (terminateStatements)
        {
            Assert.EndsWith("//", secondToLast);
        }
        else
        {
            Assert.False(secondToLast.EndsWith("//"), secondToLast);
        }
    }

    private static SchemaIdentifier SI(string schema = "myschema", string catalog = "def")
    {
        return new SchemaIdentifier(schema, new CatalogIdentifier(catalog, QuoteStyle.Ansi), QuoteStyle.Ansi);
    }

    public static ObjectName ON(string name = "mytable", string schema = "myschema", string? catalog = null)
    {
        var identifiers = new List<Identifier>();
        if (catalog != null)
        {
            identifiers.Add(new Identifier(catalog, QuoteStyle.Ansi));
        }
        identifiers.Add(new Identifier(schema, QuoteStyle.Ansi));
        identifiers.Add(new Identifier(name, QuoteStyle.Ansi));
        return new ObjectName([.. identifiers]);
    }

    private static (DifferStatementWriter Writer, StringWriter Output) ConstructWriter(bool terminateStatements = true, bool emitComments = false, bool useSchemaOnChange = true)
    {
        var formatSettings = new DifferFormattingSettings
        {
            TerminateStatements = terminateStatements,
            EmitCommentsWithWeights = emitComments,
            UseSchemaOnChange = useSchemaOnChange
        };
        StringWriter writer = new();
        return (new DifferStatementWriter(formatSettings, writer), writer);
    }

    private record FakeAlter(ObjectName Table, AlterTableOperation Operation) : AlterTable(Table, [Operation])
    {
        public override void ToSql(SqlTextWriter writer)
        {
            base.ToSql(writer);
            writer.Write(";");
        }
    }
}
