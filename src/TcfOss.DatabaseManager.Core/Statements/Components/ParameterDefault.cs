using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record ParameterDefault(bool UseDefaultKeyword) : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record ValueDefault(Value Value, bool UseDefaultKeyword) : ParameterDefault(UseDefaultKeyword)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            WriteMarker(writer);
            writer.WriteSql($"{Value}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }
    }

    public record ExpressionDefault(Expression Expression, bool UseDefaultKeyword) : ParameterDefault(UseDefaultKeyword)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            WriteMarker(writer);
            writer.WriteSql($"{Expression}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            WriteMarker(writer);
            Expression.FormatSql(writer, manager);
        }
    }

    protected void WriteMarker(SqlTextWriter writer)
    {
        writer.Write(UseDefaultKeyword ? " DEFAULT " : " = ");
    }
}
