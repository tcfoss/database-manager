using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementKeyPart() : IWriteSql
{
    public PgStatementOperatorClass? OperatorClass { get; init; }
    public NullsOrder? NullsOrder { get; init; }

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Column(Identifier Name, Direction? Direction = null) : StatementKeyPart
    {
        public uint? Length { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            Name.ToSql(writer);

            if (Length != null)
            {
                writer.WriteSql($"({Length})");
            }
            WriteTail(writer, Direction);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);

            if (Length != null)
            {
                writer.WriteSql($"({Length})");
            }
            FormatTail(writer, Direction, manager);
        }
    }

    public record IndexExpression(Expression Expression, Direction? Direction) : StatementKeyPart
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Expression}");
            WriteTail(writer, Direction);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Expression.FormatSql(writer, manager);
            FormatTail(writer, Direction, manager);
        }
    }

    private void WriteTail(SqlTextWriter writer, Direction? direction)
    {
        if (OperatorClass != null)
        {
            writer.Write(" ");
            OperatorClass.ToSql(writer);
        }
        if (direction != null)
        {
            writer.WriteSql($" {direction}");
        }
        if (NullsOrder != null)
        {
            writer.WriteSql($" NULLS {NullsOrder}");
        }
    }

    private void FormatTail(SqlTextWriter writer, Direction? direction, FormatManager manager)
    {
        if (OperatorClass != null)
        {
            writer.Write(" ");
            OperatorClass.FormatSql(writer, manager);
        }
        if (direction != null)
        {
            writer.WriteSql($" {direction}");
        }
        if (NullsOrder != null)
        {
            writer.WriteSql($" NULLS {NullsOrder}");
        }
    }
}
