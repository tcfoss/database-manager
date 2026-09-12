using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public record MyRoutineCharacteristic() : IWriteSql
{
    public Comment? Comment { get; init; }
    public string? Language { get; init; }
    public bool? Deterministic { get; init; }
    public SqlDataRelation? Relation { get; init; }
    public SecurityContext? SecurityContext { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        bool anyText = false;
        if (Comment != null)
        {
            Comment.ToSql(writer);
            anyText = true;
        }

        if (Language != null)
        {
            if (anyText)
            {
                writer.Write(" ");
            }

            writer.WriteSql($"LANGUAGE {Language}");
            anyText = true;
        }

        if (Deterministic != null)
        {
            if (anyText)
            {
                writer.Write(" ");
            }

            string isNot = !Deterministic.Value ? "NOT " : "";
            writer.WriteSql($"{isNot}DETERMINISTIC");
            anyText = true;
        }

        if (Relation != null)
        {
            if (anyText)
            {
                writer.Write(" ");
            }

            Relation.ToSql(writer);
            anyText = true;
        }

        if (SecurityContext != null)
        {
            if (anyText)
            {
                writer.Write(" ");
            }

            writer.WriteSql($"SQL SECURITY {SecurityContext}");
        }
    }
}
