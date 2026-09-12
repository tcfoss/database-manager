using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Common;

public abstract record Value() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public abstract record StringBasedValue(string Value) : Value;

    public record Boolean(bool Value) : Value
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Value ? "TRUE" : "FALSE");
        }
    }

    public record Null() : Value
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"NULL");
        }
    }

    public record Number(string Value, bool IsLong) : StringBasedValue(Value)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Value}");
            if (IsLong)
            {
                writer.Write("L");
            }
        }

        public int? AsInt()
        {
            if (int.TryParse(Value, out int val))
            {
                return val;
            }
            return null;
        }
    }

    public record SingleQuotedString(string Value) : StringBasedValue(Value)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"'{Value.EscapeSingleQuotedString()}'");
        }
    }

    public record NationalStringLiteral(string Value) : StringBasedValue(Value)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"N'{Value}'");
        }
    }

    public record HexString(string Value) : StringBasedValue(Value)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"X'{Value}'");
        }
    }
}
