using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record LabeledStatement(Statement SubStatement, Identifier Label) : Statement
{
    public record My(Statement SubStatement, Identifier Label) : LabeledStatement(SubStatement, Label)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Label}: {SubStatement}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.Write(manager.Indent);
            Label.FormatSql(writer, manager);
            writer.Write(": ");
            SubStatement.FormatSql(writer, manager);
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record Pg(Statement SubStatement, Identifier Label) : LabeledStatement(SubStatement, Label)
    {
        public MetaData LabelMeta { get; init; } = new();

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"<<{Label}>> {SubStatement}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.Write(manager.Indent);
            writer.Write("<<");
            LabelMeta.FormatPreNonSql(writer, manager);
            Label.FormatSql(writer, manager);
            LabelMeta.FormatPostNonSql(writer, manager);
            writer.Write(">>");
            manager.ExpectingNewLineBeforeStatement = true;
            SubStatement.FormatSql(writer, manager);
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in SubStatement.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
