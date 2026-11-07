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

    public record EscapeString(string Value) : StringBasedValue(Value)
    {
        protected EscapeString(EscapeString original) : base(original)
        {
        }

        /// <summary>
        /// Gets the cached decoded contents, interpreting numeric byte escapes as UTF-8.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string DecodedValue => field ??= PgStringDecoders.DecodeEscapeString(Value);

        public virtual bool Equals(EscapeString? other)
        {
            return other != null && base.Equals(other);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write($"E'{Value}'");
        }
    }

    public record UnicodeString(string Value) : StringBasedValue(Value)
    {
        public char? EscapeCharacter { get; init; }

        protected UnicodeString(UnicodeString original) : base(original)
        {
            EscapeCharacter = original.EscapeCharacter;
        }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string DecodedValue => field ??= PgStringDecoders.DecodeUnicodeString(Value, EscapeCharacter ?? '\\');

        public virtual bool Equals(UnicodeString? other)
        {
            return other != null && base.Equals(other) && EscapeCharacter == other.EscapeCharacter;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), EscapeCharacter);
        }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write($"U&'{Value}'");
            if (EscapeCharacter is { } escape)
            {
                writer.Write($" UESCAPE '{escape}'");
            }
        }
    }

    public record DollarQuotedString(string Value) : StringBasedValue(Value)
    {
        public string Tag { get; init; } = "";

        public virtual bool Equals(DollarQuotedString? other)
        {
            // Tag is excluded from equality contract
            return other != null && base.Equals(other);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write($"${Tag}${Value}${Tag}$");
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
