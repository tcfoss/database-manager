using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgExclusionElement(KeyPart Key, PgOperatorReference Operator) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Key} WITH {Operator}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Key.FormatSql(writer, manager);
        writer.Write(" WITH ");
        Operator.FormatSql(writer, manager);
    }
}
