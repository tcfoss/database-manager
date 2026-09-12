using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record KeyPart() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Column(Identifier Name, uint? Length = null, Direction? Direction = null) : KeyPart
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Name.ToSql(writer);

            if (Length != null)
            {
                writer.WriteSql($"({Length})");
            }
            if (Direction != null)
            {
                writer.WriteSql($" {Direction}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);

            if (Length != null)
            {
                writer.WriteSql($"({Length})");
            }
            if (Direction != null)
            {
                writer.WriteSql($" {Direction}");
            }
        }
    }

    public record IndexExpression(Expression Expression, Direction? Direction) : KeyPart
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Expression}");
            if (Direction != null)
            {
                writer.WriteSql($" {Direction}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Expression.FormatSql(writer, manager);
            if (Direction != null)
            {
                writer.WriteSql($" {Direction}");
            }
        }
    }

    public static SqlValueList<KeyPart> RemoveOptionalElementsFromKeyParts(SqlValueList<KeyPart> keyParts)
    {
        SqlValueList<KeyPart> result = [];
        foreach (KeyPart part in keyParts)
        {
            if (part is Column column)
            {
                result.Add(new Column(column.Name, column.Length, column.Direction == Direction.Descending ? Direction.Descending : null));
            }
            else if (part is IndexExpression indexExpression)
            {
                result.Add(new IndexExpression(indexExpression.Expression, indexExpression.Direction == Direction.Descending ? Direction.Descending : null));
            }
        }
        return result;
    }
}
