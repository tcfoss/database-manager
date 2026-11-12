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

    public record PgShell(ObjectName Name) : CreateType(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CREATE TYPE {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatHeader(writer, manager);
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record PgBase(ObjectName Name, SqlValueList<PgStatementBaseTypeOption> Options) : CreateType(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CREATE TYPE {Name} ({Options})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatHeader(writer, manager);
            writer.Write(" (");
            writer.FormatDelimited(Options, manager);
            writer.Write(")");
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record PgRange(
        ObjectName Name,
        DataTypeReference Subtype,
        ObjectName? SubtypeOpClass = null,
        ObjectName? Collation = null,
        ObjectName? Canonical = null,
        ObjectName? SubtypeDiff = null,
        ObjectName? MultirangeTypeName = null) : CreateType(Name)
    {
        private void WriteRangeOptions(SqlTextWriter writer)
        {
            writer.WriteSql($" AS RANGE (SUBTYPE = {Subtype}");
            if (SubtypeOpClass != null)
            {
                writer.WriteSql($", SUBTYPE_OPCLASS = {SubtypeOpClass}");
            }
            if (Collation != null)
            {
                writer.WriteSql($", COLLATION = {Collation}");
            }
            if (Canonical != null)
            {
                writer.WriteSql($", CANONICAL = {Canonical}");
            }
            if (SubtypeDiff != null)
            {
                writer.WriteSql($", SUBTYPE_DIFF = {SubtypeDiff}");
            }
            if (MultirangeTypeName != null)
            {
                writer.WriteSql($", MULTIRANGE_TYPE_NAME = {MultirangeTypeName}");
            }
            writer.Write(")");
        }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CREATE TYPE {Name}");
            WriteRangeOptions(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatHeader(writer, manager);
            writer.Write(" AS RANGE (SUBTYPE = ");
            Subtype.FormatSql(writer, manager);
            WriteFormattedOption(writer, manager, "SUBTYPE_OPCLASS", SubtypeOpClass);
            WriteFormattedOption(writer, manager, "COLLATION", Collation);
            WriteFormattedOption(writer, manager, "CANONICAL", Canonical);
            WriteFormattedOption(writer, manager, "SUBTYPE_DIFF", SubtypeDiff);
            WriteFormattedOption(writer, manager, "MULTIRANGE_TYPE_NAME", MultirangeTypeName);
            writer.Write(")");
            Meta.FormatPostNonSql(writer, manager);
        }

        private static void WriteFormattedOption(SqlTextWriter writer, FormatManager manager, string name, ObjectName? value)
        {
            if (value != null)
            {
                writer.Write($", {name} = ");
                value.FormatSql(writer, manager);
            }
        }
    }
}
