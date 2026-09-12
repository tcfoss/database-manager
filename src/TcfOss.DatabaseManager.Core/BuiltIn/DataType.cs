using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

#pragma warning disable CA1720 // Class names match built-in type names
#pragma warning disable CA1716 // Identifiers should not match keywords
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix

public abstract record DataType() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract DataTypeClass Class { get; }

    public virtual DataType ToDataTypeForTable(DifferFormatManager manager)
    {
        return this;
    }

    public interface IHaveOptionalPrecision
    {
        public uint? Precision { get; }

        public void WritePrecision(SqlTextWriter writer)
        {
            if (Precision != null)
            {
                writer.WriteSql($"({Precision})");
            }
        }
    }

    public abstract record CharLengthDataType(CharLength CharLength) : DataType;
    public abstract record OptionalNumericLengthDataType(NumericLength? NumericLength) : DataType;

    public abstract record BaseIntegerType : DataType
    {
        public override DataTypeClass Class => DataTypeClass.Integral;
    }

    public record TinyInt() : BaseIntegerType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TINYINT");
        }
    }

    public record Int() : BaseIntegerType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("INT");
        }
    }

    public record BigInt() : BaseIntegerType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("BIGINT");
        }
    }

    public record Boolean() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.Boolean;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("BOOLEAN");
        }
    }

    public record Decimal(NumericLength? NumericLength) : OptionalNumericLengthDataType(NumericLength)
    {
        public override DataTypeClass Class => DataTypeClass.ExactDecimal;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DECIMAL");
            if (NumericLength != null)
            {
                writer.WriteSql($"{NumericLength}");
            }
        }
    }

    public record Double(NumericLength? NumericLength) : OptionalNumericLengthDataType(NumericLength)
    {
        public override DataTypeClass Class => DataTypeClass.ApproximateDecimal;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DOUBLE");
            if (NumericLength != null)
            {
                writer.WriteSql($"{NumericLength}");
            }
        }
    }

    public record Float(NumericLength? NumericLength) : OptionalNumericLengthDataType(NumericLength)
    {
        public override DataTypeClass Class => DataTypeClass.ApproximateDecimal;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("FLOAT");
            if (NumericLength != null)
            {
                writer.WriteSql($"{NumericLength}");
            }
        }
    }

    public record Char(CharLength CharLength) : CharLengthDataType(CharLength)
    {
        public override DataTypeClass Class => DataTypeClass.String;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CHAR{CharLength}");
        }
    }

    public record Varchar(CharLength CharLength) : CharLengthDataType(CharLength)
    {
        public override DataTypeClass Class => DataTypeClass.String;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"VARCHAR{CharLength}");
        }
    }

    public record NationalVarchar(CharLength CharLength) : CharLengthDataType(CharLength)
    {
        public override DataTypeClass Class => DataTypeClass.String;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"NVARCHAR{CharLength}");
        }
    }

    public record Text() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.String;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TEXT");
        }
    }

    public record Enum(SqlValueList<string> Values) : DataType
    {
        public override DataTypeClass Class => DataTypeClass.Enum;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("ENUM(");
            for (int i = 0; i < Values.Count; i++)
            {
                if (i > 0)
                {
                    writer.Write(", ");
                }
                writer.Write($"'{Values[i].EscapeSingleQuotedString()}'");
            }
            writer.Write(")");
        }
    }

    public record Set(SqlValueList<string> Values) : DataType
    {
        public override DataTypeClass Class => DataTypeClass.Set;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SET(");
            for (int i = 0; i < Values.Count; i++)
            {
                if (i > 0)
                {
                    writer.Write(", ");
                }
                writer.Write($"'{Values[i].EscapeSingleQuotedString()}'");
            }
            writer.Write(")");
        }
    }
}
