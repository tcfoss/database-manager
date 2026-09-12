using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record AssignmentTarget() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record ObjectName(Common.ObjectName Name) : AssignmentTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Name.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            SqlValueList<Identifier> identifiers = Name.Values;

            if (manager.ExpectingQualifiedColumn)
            {
                SqlValueList<Identifier>? sourcedIdentifiers = manager.GetQualifiedSelectableItem(Name.Values);
                if (sourcedIdentifiers != null)
                {
                    identifiers = sourcedIdentifiers;
                }
            }

            Identifier[] quotedIdentifiers = manager.QuoteIdentifiers(Name.Values, identifiers);
            writer.WriteDelimited(quotedIdentifiers, ".");
        }
    }

    public record Tuple(SqlValueList<Common.ObjectName> Names) : AssignmentTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"({Names.ToSqlDelimited()})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("(");
            for (int i = 0; i < Names.Count; i++)
            {
                Identifier[] identifiers = manager.QuoteIdentifiers(Names[i].Values, Names[i].Values);
                writer.WriteDelimited(identifiers, ".");
                if (i < Names.Count - 1)
                {
                    writer.Write(", ");
                }
            }
            writer.Write(")");
        }
    }
}
