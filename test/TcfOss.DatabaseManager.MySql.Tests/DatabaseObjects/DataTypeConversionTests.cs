using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseObjects;

public class DataTypeConversionTests
{
    public static TheoryData<DataType, DataType, MySqlNumericAttribute?, bool> IntegerCases
    {
        get
        {
            var cases = new TheoryData<DataType, DataType, MySqlNumericAttribute?, bool>();
            (MyDataType.BaseMyIntegerType Type, uint SignedWidth, uint UnsignedWidth)[] types =
            [
                (new MyDataType.MyTinyInt(), 4, 3),
                (new MyDataType.MySmallInt(), 6, 5),
                (new MyDataType.MyMediumInt(), 9, 8),
                (new MyDataType.MyInt(), 11, 10),
                (new MyDataType.MyBigInt(), 20, 19)
            ];

            foreach (var (type, signedWidth, unsignedWidth) in types)
            {
                cases.Add(type, type, MySqlNumericAttribute.Signed, true);
                cases.Add(type with { Width = signedWidth }, type, MySqlNumericAttribute.Signed, true);
                cases.Add(type with { Width = unsignedWidth }, type, MySqlNumericAttribute.Unsigned, true);
                cases.Add(type with { Width = unsignedWidth }, type, null, true);
                cases.Add(type with { Width = 42 }, type with { Width = 42 }, MySqlNumericAttribute.Signed, true);
                cases.Add(type with { Width = unsignedWidth }, type with { Width = unsignedWidth }, MySqlNumericAttribute.Signed, true);
                cases.Add(type with { Width = signedWidth }, type with { Width = signedWidth }, MySqlNumericAttribute.Unsigned, true);
                cases.Add(type with { NumericAttribute = MySqlNumericAttribute.Signed, Width = signedWidth }, type, MySqlNumericAttribute.Signed, true);
                cases.Add(type with { NumericAttribute = MySqlNumericAttribute.Unsigned, Width = unsignedWidth }, type, MySqlNumericAttribute.Unsigned, true);
                cases.Add(type with { NumericAttribute = MySqlNumericAttribute.Signed, Width = signedWidth }, type with { NumericAttribute = MySqlNumericAttribute.Signed }, MySqlNumericAttribute.Unsigned, true);
                cases.Add(type with { NumericAttribute = MySqlNumericAttribute.Unsigned, Width = unsignedWidth }, type with { NumericAttribute = MySqlNumericAttribute.Unsigned }, MySqlNumericAttribute.Signed, true);
                cases.Add(type with { NumericAttribute = MySqlNumericAttribute.Zerofill, Width = unsignedWidth }, type with { NumericAttribute = MySqlNumericAttribute.Zerofill }, MySqlNumericAttribute.Signed, true);
                cases.Add(type with { NumericAttribute = MySqlNumericAttribute.Zerofill, Width = unsignedWidth }, type, MySqlNumericAttribute.Zerofill, true);

                var explicitDefaults = type with { NumericAttribute = MySqlNumericAttribute.Signed, Width = signedWidth };
                cases.Add(explicitDefaults, explicitDefaults, MySqlNumericAttribute.Signed, false);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(IntegerCases))]
    public void IntegerDefaultsApplied(DataType original, DataType expected, MySqlNumericAttribute? defaultNumericAttribute, bool omitDefaults)
    {
        var manager = GetFormatManager(defaultNumericAttribute, omitDefaults);

        var converted = original.ToDataTypeForTable(manager);

        Assert.Equal(expected, converted);
        if (!omitDefaults)
        {
            Assert.Same(original, converted);
        }
    }

    public static TheoryData<DataType, DataType, MySqlNumericAttribute?, bool> DecimalCases
    {
        get
        {
            var cases = new TheoryData<DataType, DataType, MySqlNumericAttribute?, bool>();
            (Func<NumericLength?, MySqlNumericAttribute?, DataType> Create, NumericLength DefaultLength)[] types =
            [
                ((length, attribute) => new MyDataType.MyDecimal(length) { NumericAttribute = attribute }, new NumericLength.PrecisionScale(10, 2)),
                ((length, attribute) => new MyDataType.MyDouble(length) { NumericAttribute = attribute }, new NumericLength.PrecisionScale(12, 4)),
                ((length, attribute) => new MyDataType.MyFloat(length) { NumericAttribute = attribute }, new NumericLength.PrecisionScale(8, 3))
            ];

            foreach (var (create, defaultLength) in types)
            {
                cases.Add(create(null, null), create(null, null), MySqlNumericAttribute.Signed, true);
                cases.Add(create(null, null), create(null, null), null, true);
                cases.Add(create(defaultLength, null), create(null, null), MySqlNumericAttribute.Signed, true);
                cases.Add(create(defaultLength, MySqlNumericAttribute.Signed), create(null, null), MySqlNumericAttribute.Signed, true);
                cases.Add(create(defaultLength, MySqlNumericAttribute.Unsigned), create(null, null), MySqlNumericAttribute.Unsigned, true);
                cases.Add(create(defaultLength, MySqlNumericAttribute.Unsigned), create(null, MySqlNumericAttribute.Unsigned), MySqlNumericAttribute.Signed, true);
                cases.Add(create(null, MySqlNumericAttribute.Signed), create(null, null), MySqlNumericAttribute.Signed, true);

                var precisionScale = (NumericLength.PrecisionScale)defaultLength;
                NumericLength[] nonDefaults =
                [
                    new NumericLength.Precision(precisionScale.Length),
                    precisionScale with { Length = 42 },
                    precisionScale with { Scale = 5 }
                ];
                foreach (var length in nonDefaults)
                {
                    cases.Add(create(length, MySqlNumericAttribute.Signed), create(length, null), MySqlNumericAttribute.Signed, true);
                }

                var explicitDefaults = create(defaultLength, MySqlNumericAttribute.Signed);
                cases.Add(explicitDefaults, explicitDefaults, MySqlNumericAttribute.Signed, false);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(DecimalCases))]
    public void DecimalDefaultsApplied(DataType original, DataType expected, MySqlNumericAttribute? defaultNumericAttribute, bool omitDefaults)
    {
        var manager = GetFormatManager(defaultNumericAttribute, omitDefaults);

        var converted = original.ToDataTypeForTable(manager);

        Assert.Equal(expected, converted);
        if (!omitDefaults)
        {
            Assert.Same(original, converted);
        }
    }

    public static TheoryData<TableContext, bool, string?, string?, bool, bool> StringCases
    {
        get
        {
            var cases = new TheoryData<TableContext, bool, string?, string?, bool, bool>();

            foreach (var omitDefaults in new[] { true, false })
            {
                cases.Add(TableContext.None, true, "utf8mb4", "utf8mb4_general_ci", omitDefaults, false);
                cases.Add(TableContext.Other, true, "utf8mb4", "utf8mb4_general_ci", omitDefaults, false);
                cases.Add(TableContext.MySql, false, null, null, omitDefaults, false);
                cases.Add(TableContext.MySql, true, "utf8mb4", "utf8mb4_general_ci", omitDefaults, true);
                cases.Add(TableContext.MySql, true, "latin1", "utf8mb4_general_ci", omitDefaults, false);
                cases.Add(TableContext.MySql, true, "utf8mb4", "utf8mb4_bin", omitDefaults, false);
                cases.Add(TableContext.MySql, true, "utf8mb4", null, omitDefaults, false);
                cases.Add(TableContext.MySql, true, null, "utf8mb4_general_ci", omitDefaults, false);
                cases.Add(TableContext.MySql, true, null, null, omitDefaults, false);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(StringCases))]
    public void StringDefaultsApplied(TableContext tableContext, bool hasAttributes, string? characterSet, string? collation, bool omitDefaults, bool expectOmitted)
    {
        var name = new ObjectIdentifier("example", new SchemaIdentifier("example", new CatalogIdentifier("def")));
        IHaveObjectIdentifierName? table = tableContext switch
        {
            TableContext.None => null,
            TableContext.Other => new OtherObject(name),
            TableContext.MySql => new MyTable(name, [])
            {
                Engine = "InnoDB",
                CharacterSet = "utf8mb4",
                Collation = "utf8mb4_general_ci"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(tableContext), tableContext, null)
        };
        var attributes = hasAttributes ? new StringAttribute(characterSet, collation) : null;
        var original = new MyDataType.MyVarchar(25) { StringAttribute = attributes };
        var manager = GetFormatManager(omitDefaults: omitDefaults) with { Table = table };

        var converted = original.ToDataTypeForTable(manager);

        var expected = original with { StringAttribute = expectOmitted ? null : attributes };
        Assert.Equal(expected, converted);
        if (!expectOmitted)
        {
            Assert.Same(original, converted);
        }
    }

    public enum TableContext
    {
        None,
        Other,
        MySql
    }

    private sealed record OtherObject(ObjectIdentifier Name) : IHaveObjectIdentifierName;

    private static DifferFormatManager GetFormatManager(MySqlNumericAttribute? defaultNumericAttribute = null, bool omitDefaults = true)
    {
        return new DifferFormatManager
        {
            QuoteStyle = QuoteStyle.Backticks,
            Formatting = new DifferFormattingSettings { OmitModifiersIfDefault = omitDefaults },
            AttributeDefaults = new AttributeDefaults
            {
                NumericAttribute = defaultNumericAttribute,
                UnsignedBigIntWidth = 19,
                DecimalPrecision = new NumericLength.PrecisionScale(10, 2),
                DoublePrecision = new NumericLength.PrecisionScale(12, 4),
                FloatPrecision = new NumericLength.PrecisionScale(8, 3)
            }
        };
    }
}
