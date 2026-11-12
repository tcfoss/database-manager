using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record KeyPart() : IWriteSql
{
    public PgIndexKeyOptions? PgOptions { get; init; }

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    private void WriteTail(SqlTextWriter writer, Direction? direction)
    {
        if (PgOptions?.OperatorClass != null)
        {
            writer.Write(" ");
            PgOptions.OperatorClass.ToSql(writer);
            if (PgOptions.OperatorClassParameters != null)
            {
                writer.Write(" (");
                writer.WriteDelimited(PgOptions.OperatorClassParameters);
                writer.Write(")");
            }
        }
        if (direction != null)
        {
            writer.WriteSql($" {direction}");
        }
        if (PgOptions?.NullsOrder != null)
        {
            writer.WriteSql($" NULLS {PgOptions.NullsOrder}");
        }
    }

    private void FormatTail(SqlTextWriter writer, Direction? direction, FormatManager manager)
    {
        if (PgOptions?.OperatorClass != null)
        {
            writer.Write(" ");
            PgOptions.OperatorClass.FormatSql(writer, manager);
            if (PgOptions.OperatorClassParameters != null)
            {
                writer.Write(" (");
                writer.FormatDelimited(PgOptions.OperatorClassParameters, manager);
                writer.Write(")");
            }
        }
        if (direction != null)
        {
            writer.WriteSql($" {direction}");
        }
        if (PgOptions?.NullsOrder != null)
        {
            writer.WriteSql($" NULLS {PgOptions.NullsOrder}");
        }
    }

    public record Column(Identifier Name, uint? Length = null, Direction? Direction = null) : KeyPart
    {
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

    public record IndexExpression(Expression Expression, Direction? Direction) : KeyPart
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

    public static SqlValueList<KeyPart> RemoveOptionalElementsFromKeyParts(SqlValueList<KeyPart> keyParts)
    {
        SqlValueList<KeyPart> result = [];
        foreach (KeyPart part in keyParts)
        {
            if (part is Column column)
            {
                result.Add(column with { Direction = column.Direction == Direction.Descending ? Direction.Descending : null });
            }
            else if (part is IndexExpression indexExpression)
            {
                result.Add(indexExpression with { Direction = indexExpression.Direction == Direction.Descending ? Direction.Descending : null });
            }
        }
        return result;
    }
}
