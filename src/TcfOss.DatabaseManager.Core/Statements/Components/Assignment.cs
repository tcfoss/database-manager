using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record Assignment(AssignmentTarget Target, Expression Value) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Target} = {Value}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        using (manager.Enter(FormatContext.AssignmentTarget))
        {
            Target.FormatSql(writer, manager);
        }
        writer.Write(" = ");
        using (manager.Enter(FormatContext.AssignmentValue))
        {
            Value.FormatSql(writer, manager);
        }
    }
}
