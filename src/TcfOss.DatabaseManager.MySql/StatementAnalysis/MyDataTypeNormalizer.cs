using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.StatementAnalysis;

public class MyDataTypeNormalizer(MyConfig config, string characterSet, string collation) : INormalizeDataTypes
{
    private readonly MyConfig _config = config;
    public string CharacterSet { get; set; } = characterSet;
    public string Collation { get; set; } = collation;

    private MyDataType.BaseMyIntegerType NormalizeIntegerType(MyDataType.BaseMyIntegerType intType, uint? defaultSignedWidth, uint? defaultUnsignedWidth)
    {
        if (intType.NumericAttribute == null)
        {
            intType = intType with { NumericAttribute = _config.AttributeDefaults.NumericAttribute };
        }

        if (intType.NumericAttribute == MySqlNumericAttribute.Signed && intType.Width == null && defaultSignedWidth != null)
        {
            intType = intType with { Width = defaultSignedWidth };
        }
        else if (intType.NumericAttribute == MySqlNumericAttribute.Unsigned && intType.Width == null && defaultUnsignedWidth != null)
        {
            intType = intType with { Width = defaultUnsignedWidth };
        }

        return intType;
    }

    private MyDataType.BaseMyStringType NormalizeStringType(MyDataType.BaseMyStringType strType)
    {
        if (strType.StringAttribute == null || (strType.StringAttribute.CharacterSet == null && strType.StringAttribute.Collation == null))
        {
            return strType with { StringAttribute = new StringAttribute(CharacterSet, Collation) };
        }
        if (strType.StringAttribute.CharacterSet == null)
        {
            return strType with { StringAttribute = strType.StringAttribute with { CharacterSet = CharacterSet } };
        }
        if (strType.StringAttribute.Collation == null)
        {
            return strType with { StringAttribute = strType.StringAttribute with { Collation = _config.CharacterSets[strType.StringAttribute.CharacterSet].DefaultCollation } };
        }
        return strType;
    }

    public DataType NormalizeDataType(DataType dataType)
    {
        if (dataType is MyDataType.BaseMyStringType stringType)
        {
            return NormalizeStringType(stringType);
        }

        switch (dataType)
        {
            case MyDataType.MyDecimal dec:
                if (dec.NumericAttribute == null)
                {
                    dec = dec with { NumericAttribute = _config.AttributeDefaults.NumericAttribute };
                }
                if (dec.NumericLength == null && _config.AttributeDefaults.DecimalPrecision != null)
                {
                    dec = dec with { NumericLength = _config.AttributeDefaults.DecimalPrecision };
                }
                return dec;

            case MyDataType.MyFloat fl:
                if (fl.NumericAttribute == null)
                {
                    fl = fl with { NumericAttribute = _config.AttributeDefaults.NumericAttribute };
                }
                if (fl.NumericLength == null && _config.AttributeDefaults.FloatPrecision != null)
                {
                    fl = fl with { NumericLength = _config.AttributeDefaults.FloatPrecision };
                }
                return fl;

            case MyDataType.MyDouble dbl:
                if (dbl.NumericAttribute == null)
                {
                    dbl = dbl with { NumericAttribute = _config.AttributeDefaults.NumericAttribute };
                }
                if (dbl.NumericLength == null && _config.AttributeDefaults.DoublePrecision != null)
                {
                    dbl = dbl with { NumericLength = _config.AttributeDefaults.DoublePrecision };
                }
                return dbl;

            case MyDataType.MyTinyInt ti:
                return NormalizeIntegerType(ti, _config.AttributeDefaults.SignedTinyIntWidth, _config.AttributeDefaults.UnsignedTinyIntWidth);

            case MyDataType.MySmallInt si:
                return NormalizeIntegerType(si, _config.AttributeDefaults.SignedSmallIntWidth, _config.AttributeDefaults.UnsignedSmallIntWidth);

            case MyDataType.MyMediumInt mi:
                return NormalizeIntegerType(mi, _config.AttributeDefaults.SignedMediumIntWidth, _config.AttributeDefaults.UnsignedMediumIntWidth);

            case MyDataType.MyInt i:
                return NormalizeIntegerType(i, _config.AttributeDefaults.SignedIntWidth, _config.AttributeDefaults.UnsignedIntWidth);

            case MyDataType.MyBigInt bi:
                return NormalizeIntegerType(bi, _config.AttributeDefaults.SignedBigIntWidth, _config.AttributeDefaults.UnsignedBigIntWidth);

            case MyDataType.Year yr:
                if (yr.Precision == null && _config.AttributeDefaults.YearPrecision != null)
                {
                    yr = yr with { Precision = _config.AttributeDefaults.YearPrecision };
                }
                return yr;
        }
        return dataType;
    }
}
