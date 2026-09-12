using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.MsSql.BuiltIn;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
#pragma warning disable CA1716 // Identifiers should not match keywords
#pragma warning disable CA1720 // Identifiers should not contain type names

/// <summary>
/// Pseudo-namespace for Microsoft SQL Server-specific data types. Types that
/// have no useful Core equivalent live here; types that map cleanly to Core
/// (e.g. INT, BIGINT, VARCHAR, NVARCHAR) reuse the Core records directly.
/// </summary>
public abstract record MsDataType() : DataType
{
    public record MsSmallInt() : BaseIntegerType
    {
        public override void ToSql(SqlTextWriter writer) => writer.Write("SMALLINT");
    }

    /// <summary>
    /// T-SQL <c>BIT</c> — 0/1/NULL integer-like type.
    /// </summary>
    public record MsBit() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.Boolean;
        public override void ToSql(SqlTextWriter writer) => writer.Write("BIT");
    }

    public record MsMoney() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.ExactDecimal;
        public override void ToSql(SqlTextWriter writer) => writer.Write("MONEY");
    }

    public record MsSmallMoney() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.ExactDecimal;
        public override void ToSql(SqlTextWriter writer) => writer.Write("SMALLMONEY");
    }

    public record MsDate() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.DateTime;
        public override void ToSql(SqlTextWriter writer) => writer.Write("DATE");
    }

    public record MsTime(uint? Precision = null) : DataType, IHaveOptionalPrecision
    {
        public override DataTypeClass Class => DataTypeClass.DateTime;
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TIME");
            ((IHaveOptionalPrecision)this).WritePrecision(writer);
        }
    }

    public record MsDateTime() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.DateTime;
        public override void ToSql(SqlTextWriter writer) => writer.Write("DATETIME");
    }

    public record MsSmallDateTime() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.DateTime;
        public override void ToSql(SqlTextWriter writer) => writer.Write("SMALLDATETIME");
    }

    public record MsDateTime2(uint? Precision = null) : DataType, IHaveOptionalPrecision
    {
        public override DataTypeClass Class => DataTypeClass.DateTime;
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DATETIME2");
            ((IHaveOptionalPrecision)this).WritePrecision(writer);
        }
    }

    public record MsDateTimeOffset(uint? Precision = null) : DataType, IHaveOptionalPrecision
    {
        public override DataTypeClass Class => DataTypeClass.DateTime;
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DATETIMEOFFSET");
            ((IHaveOptionalPrecision)this).WritePrecision(writer);
        }
    }

    public record MsUniqueIdentifier() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.String;
        public override void ToSql(SqlTextWriter writer) => writer.Write("UNIQUEIDENTIFIER");
    }

    /// <summary>
    /// T-SQL <c>ROWVERSION</c> (synonym for the deprecated <c>TIMESTAMP</c>);
    /// an 8-byte automatically-updated binary number.
    /// </summary>
    public record MsRowVersion() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.Binary;
        public override void ToSql(SqlTextWriter writer) => writer.Write("ROWVERSION");
    }

    /// <summary>
    /// Legacy T-SQL <c>IMAGE</c> (variable-length binary, up to 2GB).
    /// </summary>
    public record MsImage() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.Binary;
        public override void ToSql(SqlTextWriter writer) => writer.Write("IMAGE");
    }

    /// <summary>
    /// Legacy T-SQL <c>NTEXT</c> (variable-length Unicode, up to 2GB).
    /// </summary>
    public record MsNText() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.String;
        public override void ToSql(SqlTextWriter writer) => writer.Write("NTEXT");
    }

    /// <summary>
    /// T-SQL <c>NCHAR(n)</c>.
    /// </summary>
    public record MsNChar(CharLength CharLength) : CharLengthDataType(CharLength)
    {
        public override DataTypeClass Class => DataTypeClass.String;
        public override void ToSql(SqlTextWriter writer) => writer.WriteSql($"NCHAR{CharLength}");
    }

    /// <summary>
    /// T-SQL <c>BINARY(n)</c>.
    /// </summary>
    public record MsBinary(CharLength CharLength) : CharLengthDataType(CharLength)
    {
        public override DataTypeClass Class => DataTypeClass.Binary;
        public override void ToSql(SqlTextWriter writer) => writer.WriteSql($"BINARY{CharLength}");
    }

    /// <summary>
    /// T-SQL <c>VARBINARY(n|MAX)</c>.
    /// </summary>
    public record MsVarBinary(CharLength CharLength) : CharLengthDataType(CharLength)
    {
        public override DataTypeClass Class => DataTypeClass.Binary;
        public override void ToSql(SqlTextWriter writer) => writer.WriteSql($"VARBINARY{CharLength}");
    }
}
