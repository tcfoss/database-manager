using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.BuiltIn;

#pragma warning disable CA1711 // Ends in 'Collection'
#pragma warning disable CA1716 // Reserved language keywords
#pragma warning disable CA1720 // Identifier 'signed' contains type name

public abstract record MyDataType() : DataType
{
    public abstract record BaseMyIntegerType() : BaseIntegerType
    {
        public uint? Width { get; init; }
        public MySqlNumericAttribute? NumericAttribute { get; init; }

        protected void AddAttributes(SqlTextWriter writer)
        {
            if (Width != null)
            {
                writer.Write($"({Width.Value})");
            }
            if (NumericAttribute != null)
            {
                writer.Write($" {NumericAttribute}");
            }
        }

        public override DataType ToDataTypeForTable(DifferFormatManager manager)
        {
            if (!manager.Formatting.OmitModifiersIfDefault)
            {
                return this;
            }

            BaseMyIntegerType dataType = this;
            bool signed = NumericAttribute == MySqlNumericAttribute.Signed || (NumericAttribute == null && manager.AttributeDefaults.NumericAttribute == MySqlNumericAttribute.Signed);

            if (NumericAttribute == manager.AttributeDefaults.NumericAttribute)
            {
                dataType = dataType with { NumericAttribute = null };
            }

            dataType = dataType with
            {
                Width = GetUpdatedWidth(manager, signed)
            };
            return dataType;
        }

        protected abstract uint? GetUpdatedWidth(DifferFormatManager manager, bool signed);
    }

    public record MyTinyInt() : BaseMyIntegerType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TINYINT");
            AddAttributes(writer);
        }

        protected override uint? GetUpdatedWidth(DifferFormatManager manager, bool signed)
        {
            return Width == (signed ? manager.AttributeDefaults.SignedTinyIntWidth : manager.AttributeDefaults.UnsignedTinyIntWidth) ? null : Width;
        }
    }

    public record MySmallInt() : BaseMyIntegerType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SMALLINT");
            AddAttributes(writer);
        }

        protected override uint? GetUpdatedWidth(DifferFormatManager manager, bool signed)
        {
            return Width == (signed ? manager.AttributeDefaults.SignedSmallIntWidth : manager.AttributeDefaults.UnsignedSmallIntWidth) ? null : Width;
        }
    }

    public record MyMediumInt() : BaseMyIntegerType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("MEDIUMINT");
            AddAttributes(writer);
        }

        protected override uint? GetUpdatedWidth(DifferFormatManager manager, bool signed)
        {
            return Width == (signed ? manager.AttributeDefaults.SignedMediumIntWidth : manager.AttributeDefaults.UnsignedMediumIntWidth) ? null : Width;
        }
    }

    public record MyInt() : BaseMyIntegerType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("INT");
            AddAttributes(writer);
        }

        protected override uint? GetUpdatedWidth(DifferFormatManager manager, bool signed)
        {
            return Width == (signed ? manager.AttributeDefaults.SignedIntWidth : manager.AttributeDefaults.UnsignedIntWidth) ? null : Width;
        }
    }

    public record MyBigInt() : BaseMyIntegerType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("BIGINT");
            AddAttributes(writer);
        }

        protected override uint? GetUpdatedWidth(DifferFormatManager manager, bool signed)
        {
            return Width == (signed ? manager.AttributeDefaults.SignedBigIntWidth : manager.AttributeDefaults.UnsignedBigIntWidth) ? null : Width;
        }
    }

    public record MyDecimal(NumericLength? NumericLength) : Decimal(NumericLength)
    {
        public override DataTypeClass Class => DataTypeClass.ExactDecimal;

        public MySqlNumericAttribute? NumericAttribute { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            base.ToSql(writer);
            if (NumericAttribute != null)
            {
                writer.WriteSql($" {NumericAttribute}");
            }
        }

        public override DataType ToDataTypeForTable(DifferFormatManager manager)
        {
            if (manager.Formatting.OmitModifiersIfDefault)
            {
                AttributeDefaults defaults = manager.AttributeDefaults;
                MyDecimal dataType = this;
                if (NumericAttribute == defaults.NumericAttribute)
                {
                    dataType = dataType with { NumericAttribute = null };
                }
                if (dataType.NumericLength != null && dataType.NumericLength == defaults.DecimalPrecision)
                {
                    dataType = dataType with { NumericLength = null };
                }
                return dataType;
            }
            return base.ToDataTypeForTable(manager);
        }
    }

    public record MyDouble(NumericLength? NumericLength) : Double(NumericLength)
    {
        public override DataTypeClass Class => DataTypeClass.ApproximateDecimal;

        public MySqlNumericAttribute? NumericAttribute { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            base.ToSql(writer);
            if (NumericAttribute != null)
            {
                writer.WriteSql($" {NumericAttribute}");
            }
        }

        public override DataType ToDataTypeForTable(DifferFormatManager manager)
        {
            if (manager.Formatting.OmitModifiersIfDefault)
            {
                AttributeDefaults defaults = manager.AttributeDefaults;
                MyDouble dataType = this;
                if (NumericAttribute == defaults.NumericAttribute)
                {
                    dataType = dataType with { NumericAttribute = null };
                }
                if (dataType.NumericLength != null && dataType.NumericLength == defaults.DoublePrecision)
                {
                    dataType = dataType with { NumericLength = null };
                }
                return dataType;
            }
            return base.ToDataTypeForTable(manager);
        }
    }

    public record MyFloat(NumericLength? NumericLength) : Float(NumericLength)
    {
        public override DataTypeClass Class => DataTypeClass.ApproximateDecimal;

        public MySqlNumericAttribute? NumericAttribute { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            base.ToSql(writer);
            if (NumericAttribute != null)
            {
                writer.WriteSql($" {NumericAttribute}");
            }
        }

        public override DataType ToDataTypeForTable(DifferFormatManager manager)
        {
            if (manager.Formatting.OmitModifiersIfDefault)
            {
                AttributeDefaults defaults = manager.AttributeDefaults;
                MyFloat dataType = this;
                if (NumericAttribute == defaults.NumericAttribute)
                {
                    dataType = dataType with { NumericAttribute = null };
                }
                if (dataType.NumericLength != null && dataType.NumericLength == defaults.FloatPrecision)
                {
                    dataType = dataType with { NumericLength = null };
                }
                return dataType;
            }
            return base.ToDataTypeForTable(manager);
        }
    }

    public abstract record BaseMyStringType() : DataType
    {
        public override DataTypeClass Class => DataTypeClass.String;

        public StringAttribute? StringAttribute { get; init; }

        protected void AddAttributes(SqlTextWriter writer)
        {
            if (StringAttribute is { EitherSet: true })
            {
                writer.WriteSql($" {StringAttribute}");
            }
        }

        public override DataType ToDataTypeForTable(DifferFormatManager manager)
        {
            if (manager.Table is MyTable myTable && StringAttribute != null)
            {
                var tableStringAttribute = new StringAttribute(myTable.CharacterSet, myTable.Collation);
                if (StringAttribute == tableStringAttribute)
                {
                    return this with { StringAttribute = null };
                }
            }
            return base.ToDataTypeForTable(manager);
        }
    }

    public abstract record BaseMyLengthedStringType(uint Length) : BaseMyStringType;

    public record MyChar(uint Length) : BaseMyLengthedStringType(Length)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CHAR({Length})");
            AddAttributes(writer);
        }
    }

    public record MyCharOptionalLength(uint? Length) : BaseMyStringType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("CHAR");
            if (Length != null)
            {
                writer.WriteSql($"({Length.Value})");
            }
            AddAttributes(writer);
        }
    }

    public record MyVarchar(uint Length) : BaseMyLengthedStringType(Length)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"VARCHAR({Length})");
            AddAttributes(writer);
        }
    }

    public record MyVarcharOptionalLength(uint? Length) : BaseMyStringType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("VARCHAR");
            if (Length != null)
            {
                writer.WriteSql($"({Length.Value})");
            }
            AddAttributes(writer);
        }
    }

    public record MyNationalVarchar(uint Length) : BaseMyLengthedStringType(Length)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"NATIONAL VARCHAR({Length})");
            AddAttributes(writer);
        }
    }

    public record MyNationalVarcharOptionalLength(uint? Length) : BaseMyStringType
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("NATIONAL VARCHAR");
            if (Length != null)
            {
                writer.WriteSql($"({Length.Value})");
            }
            AddAttributes(writer);
        }
    }

    public record MyTinyText() : BaseMyStringType
    {
        public uint? Length { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TINYTEXT");
            if (Length != null)
            {
                writer.WriteSql($"({Length.Value})");
            }
            AddAttributes(writer);
        }
    }

    public record MyText() : BaseMyStringType
    {
        public uint? Length { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TEXT");
            if (Length != null)
            {
                writer.WriteSql($"({Length.Value})");
            }
            AddAttributes(writer);
        }
    }

    public record MyMediumText() : BaseMyStringType
    {
        public uint? Length { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("MEDIUMTEXT");
            if (Length != null)
            {
                writer.WriteSql($"({Length.Value})");
            }
            AddAttributes(writer);
        }
    }

    public record MyLongText() : BaseMyStringType
    {
        public uint? Length { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("LONGTEXT");
            if (Length != null)
            {
                writer.WriteSql($"({Length.Value})");
            }
            AddAttributes(writer);
        }
    }

    public record Date() : MyDataType
    {
        public override DataTypeClass Class => DataTypeClass.Date;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DATE");
        }
    }

    public record DateTime() : MyDataType, IHaveOptionalPrecision
    {
        public override DataTypeClass Class => DataTypeClass.DateTime;

        public uint? Precision { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DATETIME");
            ((IHaveOptionalPrecision)this).WritePrecision(writer);
        }
    }

    public record Time() : MyDataType, IHaveOptionalPrecision
    {
        public override DataTypeClass Class => DataTypeClass.Time;

        public uint? Precision { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TIME");
            ((IHaveOptionalPrecision)this).WritePrecision(writer);
        }
    }

    public record TimeStamp() : MyDataType, IHaveOptionalPrecision
    {
        public override DataTypeClass Class => DataTypeClass.DateTime;

        public uint? Precision { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TIMESTAMP");
            ((IHaveOptionalPrecision)this).WritePrecision(writer);
        }
    }

    public record Year() : MyDataType, IHaveOptionalPrecision
    {
        public override DataTypeClass Class => DataTypeClass.Year;

        public uint? Precision { get; init; }
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("YEAR");
            ((IHaveOptionalPrecision)this).WritePrecision(writer);
        }
    }

    public record Binary(uint Length) : MyDataType
    {
        public override DataTypeClass Class => DataTypeClass.Binary;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"BINARY({Length})");
        }
    }

    public record Varbinary(uint Length) : MyDataType
    {
        public override DataTypeClass Class => DataTypeClass.Binary;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"VARBINARY({Length})");
        }
    }

    public record TinyBlob() : MyDataType
    {
        public override DataTypeClass Class => DataTypeClass.Binary;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TINYBLOB");
        }
    }

    public record Blob() : MyDataType
    {
        public override DataTypeClass Class => DataTypeClass.Binary;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("BLOB");
        }
    }

    public record MediumBlob() : MyDataType
    {
        public override DataTypeClass Class => DataTypeClass.Binary;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("MEDIUMBLOB");
        }
    }

    public record LongBlob() : MyDataType
    {
        public override DataTypeClass Class => DataTypeClass.Binary;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("LONGBLOB");
        }
    }

    public record Geometry() : MyDataType
    {
        public override DataTypeClass Class => DataTypeClass.Geometry;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("GEOMETRY");
        }

        public record Point() : Geometry
        {
            public override void ToSql(SqlTextWriter writer)
            {
                writer.Write("POINT");
            }
        }

        public record Curve() : Geometry
        {
            public override void ToSql(SqlTextWriter writer)
            {
                writer.Write("CURVE");
            }

            public record LineString() : Curve
            {
                public override void ToSql(SqlTextWriter writer)
                {
                    writer.Write("LINESTRING");
                }

                public record Line() : LineString
                {
                    public override void ToSql(SqlTextWriter writer)
                    {
                        writer.Write("LINE");
                    }
                }

                public record LinearRing() : LineString
                {
                    public override void ToSql(SqlTextWriter writer)
                    {
                        writer.Write("LINEARRING");
                    }
                }
            }
        }

        public record Surface() : Geometry
        {
            public override void ToSql(SqlTextWriter writer)
            {
                writer.Write("SURFACE");
            }

            public record Polygon() : Surface
            {
                public override void ToSql(SqlTextWriter writer)
                {
                    writer.Write("POLYGON");
                }
            }
        }

        public record GeometryCollection() : Geometry
        {
            public override void ToSql(SqlTextWriter writer)
            {
                writer.Write("GEOMETRYCOLLECTION");
            }

            public record MultiPoint() : GeometryCollection
            {
                public override void ToSql(SqlTextWriter writer)
                {
                    writer.Write("MULTIPOINT");
                }
            }

            public record MultiCurve() : GeometryCollection
            {
                public override void ToSql(SqlTextWriter writer)
                {
                    writer.Write("MULTICURVE");
                }

                public record MultiLineString() : MultiCurve
                {
                    public override void ToSql(SqlTextWriter writer)
                    {
                        writer.Write("MULTILINESTRING");
                    }
                }
            }

            public record MultiSurface() : GeometryCollection
            {
                public override void ToSql(SqlTextWriter writer)
                {
                    writer.Write("MULTISURFACE");
                }

                public record MultiPolygon() : MultiSurface
                {
                    public override void ToSql(SqlTextWriter writer)
                    {
                        writer.Write("MULTIPOLYGON");
                    }
                }
            }
        }
    }
}
