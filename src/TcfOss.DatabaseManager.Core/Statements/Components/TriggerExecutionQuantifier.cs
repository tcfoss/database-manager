using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record TriggerExecutionQuantifier : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record ForEach(TriggerForEachType Type) : TriggerExecutionQuantifier
    {
        public bool UseEach { get; init; } = true;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"FOR{(UseEach ? " EACH" : "")} {Type}");
        }
    }
}
