using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.StatementAnalysis;

namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding;

public class MyNormalizerTests
{
    [Theory]
    [InlineData(false, false, null, null)]
    [InlineData(true, false, null, null)]
    [InlineData(false, true, null, null)]
    [InlineData(true, true, null, null)]
    [InlineData(true, false, "latin1", "latin1_general_ci")]
    [InlineData(true, true, "latin1", "latin1_general_ci")]
    [InlineData(false, true, "latin1", null)]
    [InlineData(false, true, null, "utf8mb4_general_ci")]
    [InlineData(true, true, null, "utf8mb4_general_ci")]
    public void NormalizeCast(bool convertVarchar, bool addCharset, string? charset, string? collation)
    {
        var config = TestConfig.GetMyTestConfig(normalizationSettings: new NormalizationSettings
        {
            CastConvertVarcharToChar = convertVarchar,
            CastAddCharsetToType = addCharset,
        });
        var normalizer = new MyNormalizer(config, new MyComponentNormalizer(config.QuoteStyle, new MyFunctionNameProvider()));
        var attribute = charset != null || collation != null ? new StringAttribute(charset, collation) : null;
        var dataType = new MyDataType.MyVarcharOptionalLength(12) { StringAttribute = attribute };
        var cast = new Cast(new SingleIdentifier(new Identifier("value")), dataType);

        var actual = Assert.IsType<Cast>(normalizer.NormalizeExpression(cast));

        Assert.Equal((uint?)12,
            convertVarchar
                ? Assert.IsType<MyDataType.MyCharOptionalLength>(actual.DataType).Length
                : Assert.IsType<MyDataType.MyVarcharOptionalLength>(actual.DataType).Length);

        var actualType = Assert.IsType<MyDataType.BaseMyStringType>(actual.DataType, exactMatch: false);
        Assert.Equal(charset ?? (addCharset ? config.DatabaseCredentials.DefaultCharset : null), actualType.StringAttribute?.CharacterSet);
        Assert.Equal(collation, actualType.StringAttribute?.Collation);
        Assert.Equal(new SingleIdentifier(new Identifier("value", config.QuoteStyle)), actual.Expression);
        Assert.Same(dataType, cast.DataType);
        Assert.Same(attribute, dataType.StringAttribute);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalizeCast_PreservesNonVarcharType(bool stringType)
    {
        var config = TestConfig.GetMyTestConfig(normalizationSettings: new NormalizationSettings
        {
            CastConvertVarcharToChar = true,
            CastAddCharsetToType = true,
        });
        var normalizer = new MyNormalizer(config, new MyComponentNormalizer(config.QuoteStyle, new MyFunctionNameProvider()));
        DataType dataType = stringType ? new MyDataType.MyCharOptionalLength(null) : new MyDataType.MyInt();
        var cast = new Cast(new LiteralValue(new Value.Number("1", false)), dataType);

        var actual = Assert.IsType<Cast>(normalizer.NormalizeExpression(cast));

        if (stringType)
        {
            var charType = Assert.IsType<MyDataType.MyCharOptionalLength>(actual.DataType);
            Assert.Null(charType.Length);
            Assert.NotNull(charType.StringAttribute);
            Assert.Equal(config.DatabaseCredentials.DefaultCharset, charType.StringAttribute.CharacterSet);
        }
        else
        {
            Assert.Equal(dataType, actual.DataType);
        }
        Assert.Equal(cast.Expression, actual.Expression);
    }
}
