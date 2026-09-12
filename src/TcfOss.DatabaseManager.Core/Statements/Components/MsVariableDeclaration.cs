
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record MsVariableDeclaration(Identifier Name, DataType DataType) : IWriteSql
{
    public Expression? InitialValue { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Name} {DataType}");
        if (InitialValue != null)
        {
            writer.WriteSql($" = {InitialValue}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Name.FormatSql(writer, manager);
        writer.WriteSql($" {DataType}");
        if (InitialValue != null)
        {
            writer.Write(" = ");
            InitialValue.FormatSql(writer, manager);
        }
    }
}
