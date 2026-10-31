using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Postgres.BuiltIn;

public abstract record PgDataType : DataType
{
    public abstract record PgSimpleType(string SqlName, DataTypeClass TypeClass) : DataType
    {
        public override DataTypeClass Class => TypeClass;
        public override void ToSql(SqlTextWriter writer) => writer.Write(SqlName);
    }

    public record PgSmallInt() : PgSimpleType("SMALLINT", DataTypeClass.Integral);
    public record PgSmallSerial() : PgSimpleType("SMALLSERIAL", DataTypeClass.Integral);
    public record PgSerial() : PgSimpleType("SERIAL", DataTypeClass.Integral);
    public record PgBigSerial() : PgSimpleType("BIGSERIAL", DataTypeClass.Integral);
    public record PgReal() : PgSimpleType("REAL", DataTypeClass.ApproximateDecimal);
    public record PgDoublePrecision() : PgSimpleType("DOUBLE PRECISION", DataTypeClass.ApproximateDecimal);
    public record PgMoney() : PgSimpleType("MONEY", DataTypeClass.ExactDecimal);
    public record PgBytea() : PgSimpleType("BYTEA", DataTypeClass.Binary);
    public record PgDate() : PgSimpleType("DATE", DataTypeClass.Date);
    public record PgUuid() : PgSimpleType("UUID", DataTypeClass.Guid);
    public record PgXml() : PgSimpleType("XML", DataTypeClass.Xml);
    public record PgJson() : PgSimpleType("JSON", DataTypeClass.Json);
    public record PgJsonb() : PgSimpleType("JSONB", DataTypeClass.Json);
    public record PgJsonPath() : PgSimpleType("JSONPATH", DataTypeClass.Json);
    public record PgInet() : PgSimpleType("INET", DataTypeClass.Network);
    public record PgCidr() : PgSimpleType("CIDR", DataTypeClass.Network);
    public record PgMacAddr() : PgSimpleType("MACADDR", DataTypeClass.Network);
    public record PgMacAddr8() : PgSimpleType("MACADDR8", DataTypeClass.Network);
    public record PgPoint() : PgSimpleType("POINT", DataTypeClass.Geometry);
    public record PgLine() : PgSimpleType("LINE", DataTypeClass.Geometry);
    public record PgLSeg() : PgSimpleType("LSEG", DataTypeClass.Geometry);
    public record PgBox() : PgSimpleType("BOX", DataTypeClass.Geometry);
    public record PgPath() : PgSimpleType("PATH", DataTypeClass.Geometry);
    public record PgPolygon() : PgSimpleType("POLYGON", DataTypeClass.Geometry);
    public record PgCircle() : PgSimpleType("CIRCLE", DataTypeClass.Geometry);
    public record PgTsVector() : PgSimpleType("TSVECTOR", DataTypeClass.TextSearch);
    public record PgTsQuery() : PgSimpleType("TSQUERY", DataTypeClass.TextSearch);
    public record PgLsn() : PgSimpleType("PG_LSN", DataTypeClass.System);
    public record PgSnapshot() : PgSimpleType("PG_SNAPSHOT", DataTypeClass.System);
    public record PgTxidSnapshot() : PgSimpleType("TXID_SNAPSHOT", DataTypeClass.System);
    public record PgInt4Range() : PgSimpleType("INT4RANGE", DataTypeClass.Range);
    public record PgInt8Range() : PgSimpleType("INT8RANGE", DataTypeClass.Range);
    public record PgNumRange() : PgSimpleType("NUMRANGE", DataTypeClass.Range);
    public record PgTsRange() : PgSimpleType("TSRANGE", DataTypeClass.Range);
    public record PgTstzRange() : PgSimpleType("TSTZRANGE", DataTypeClass.Range);
    public record PgDateRange() : PgSimpleType("DATERANGE", DataTypeClass.Range);
    public record PgInt4Multirange() : PgSimpleType("INT4MULTIRANGE", DataTypeClass.Multirange);
    public record PgInt8Multirange() : PgSimpleType("INT8MULTIRANGE", DataTypeClass.Multirange);
    public record PgNumMultirange() : PgSimpleType("NUMMULTIRANGE", DataTypeClass.Multirange);
    public record PgTsMultirange() : PgSimpleType("TSMULTIRANGE", DataTypeClass.Multirange);
    public record PgTstzMultirange() : PgSimpleType("TSTZMULTIRANGE", DataTypeClass.Multirange);
    public record PgDateMultirange() : PgSimpleType("DATEMULTIRANGE", DataTypeClass.Multirange);

    public record PgNumeric(uint? Precision = null, int? Scale = null) : DataType
    {
        public override DataTypeClass Class => DataTypeClass.ExactDecimal;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("NUMERIC");
            if (Precision != null)
            {
                writer.WriteSql($"({Precision}");
                if (Scale != null)
                {
                    writer.WriteSql($",{Scale}");
                }
                writer.Write(")");
            }
        }
    }

    public abstract record PgLengthType(string SqlName, DataTypeClass TypeClass, uint? Length) : DataType
    {
        public override DataTypeClass Class => TypeClass;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(SqlName);
            if (Length != null)
            {
                writer.WriteSql($"({Length})");
            }
        }
    }

    public record PgChar(uint? Length = null) : PgLengthType("CHAR", DataTypeClass.String, Length);
    public record PgVarchar(uint? Length = null) : PgLengthType("VARCHAR", DataTypeClass.String, Length);
    public record PgBpchar(uint? Length = null) : PgLengthType("BPCHAR", DataTypeClass.String, Length);
    public record PgBit(uint? Length = null) : PgLengthType("BIT", DataTypeClass.Binary, Length);
    public record PgVarbit(uint? Length = null) : PgLengthType("BIT VARYING", DataTypeClass.Binary, Length);

    public abstract record PgTemporalType(string SqlName, DataTypeClass TypeClass, uint? Precision, bool? WithTimeZone)
        : DataType, IHaveOptionalPrecision
    {
        public override DataTypeClass Class => TypeClass;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(SqlName);
            ((IHaveOptionalPrecision)this).WritePrecision(writer);
            if (WithTimeZone != null)
            {
                writer.Write(WithTimeZone.Value ? " WITH TIME ZONE" : " WITHOUT TIME ZONE");
            }
        }
    }

    public record PgTime(uint? Precision = null, bool? WithTimeZone = null)
        : PgTemporalType("TIME", DataTypeClass.Time, Precision, WithTimeZone);
    public record PgTimestamp(uint? Precision = null, bool? WithTimeZone = null)
        : PgTemporalType("TIMESTAMP", DataTypeClass.DateTime, Precision, WithTimeZone);

    public record PgInterval(PgIntervalField? StartField = null, PgIntervalField? EndField = null, uint? Precision = null)
        : DataType, IHaveOptionalPrecision
    {
        public override DataTypeClass Class => DataTypeClass.Interval;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("INTERVAL");
            if (StartField != null)
            {
                writer.Write($" {StartField}");
                if (EndField != null)
                {
                    writer.Write($" TO {EndField}");
                }
            }
            ((IHaveOptionalPrecision)this).WritePrecision(writer);
        }
    }

    public record Custom(ObjectIdentifier Name) : DataType
    {
        public override DataTypeClass Class => DataTypeClass.Custom;

        public override void ToSql(SqlTextWriter writer)
        {
            Name.ToObjectName(2).ToSql(writer);
        }
    }
}
