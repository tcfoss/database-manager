using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record CreateType(ObjectName Name) : Statement, ICreateStatement
{
    protected void FormatHeader(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSql($"{manager.Indent}CREATE TYPE ");
        Name.FormatSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }

    public record Enumerated(ObjectName Name, SqlValueList<string> Labels) : CreateType(Name)
    {
        private void WriteLabels(SqlTextWriter writer)
        {
            writer.Write(" AS ENUM (");
            writer.WriteDelimited(Labels.Select(label => new Value.SingleQuotedString(label)));
            writer.Write(")");
        }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CREATE TYPE {Name}");
            WriteLabels(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatHeader(writer, manager);
            WriteLabels(writer);
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record Composite(ObjectName Name, SqlValueList<CompositeTypeField> Attributes) : CreateType(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CREATE TYPE {Name} AS ({Attributes})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatHeader(writer, manager);
            writer.Write(" AS (");
            writer.FormatDelimited(Attributes, manager);
            writer.Write(")");
            Meta.FormatPostNonSql(writer, manager);
        }
    }
}
