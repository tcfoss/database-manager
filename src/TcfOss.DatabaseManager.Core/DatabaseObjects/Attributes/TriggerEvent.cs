using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

public abstract record TriggerEvent : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public static TriggerEvent Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.ToUpperInvariant() switch
        {
            "INSERT" => new Insert(),
            "UPDATE" => new Update(),
            "DELETE" => new Delete(),
            "TRUNCATE" => new Truncate(),
            _ => throw new ArgumentException(null, nameof(text))
        };
    }

    public record Insert : TriggerEvent
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("INSERT");
        }
    }

    public record Update(SqlValueList<Identifier>? Columns = null) : TriggerEvent
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("UPDATE");
            if (Columns != null)
            {
                writer.WriteSql($" OF {Columns}");
            }
        }
    }

    public record Delete : TriggerEvent
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DELETE");
        }
    }

    public record Truncate : TriggerEvent
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TRUNCATE");
        }
    }
}
