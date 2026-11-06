using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyNormalizer(MyConfig config, ComponentNormalizer componentNormalizer)
    : Normalizer(config, componentNormalizer)
{
    protected override INormalizeSql ConstructNewNormalizer()
    {
        return new MyNormalizer((MyConfig)Config, ComponentNormalizer);
    }

    protected override FunctionCall NormalizeFunctionCall(FunctionCall functionCall, bool inSelect = false, bool flatten = false)
    {
        FunctionCall normalized = base.NormalizeFunctionCall(functionCall, inSelect, flatten);
        if (functionCall.Name.Values.Count == 1 && functionCall.Name.Values[0].Name.Equals("NOW", StringComparison.OrdinalIgnoreCase))
        {
            return normalized with { Name = new ObjectName(new Identifier("CURRENT_TIMESTAMP")) };
        }
        return normalized;
    }

    protected override Exists NormalizeExists(Exists exists, bool flatten = false)
    {
        return base.NormalizeExists(exists with { Subquery = exists.Subquery with { Limit = null } }, flatten);
    }

    protected override Expression NormalizeIsFalse(IsFalse isFalse, bool inSelect = false, bool flatten = false)
    {
        if (isFalse is { Negated: false, Expression: Exists { Negated: false } exists })
        {
            return NormalizeExists(exists with { Negated = true }, flatten);
        }
        return base.NormalizeIsFalse(isFalse, inSelect, flatten);
    }

    protected override Expression NormalizeCast(Cast cast, bool inSelect = false, bool flatten = false)
    {
        DataType dataType = ((DataTypeReference.BuiltIn)cast.DataType).DataType;
        if (dataType is MyDataType.MyVarcharOptionalLength varcharType && Config.NormalizationSettings.CastConvertVarcharToChar)
        {
            dataType = new MyDataType.MyCharOptionalLength(varcharType.Length)
            {
                StringAttribute = varcharType.StringAttribute
            };
        }

        if (dataType is MyDataType.BaseMyStringType strType && Config.NormalizationSettings.CastAddCharsetToType)
        {
            if (strType.StringAttribute == null)
            {
                strType = strType with { StringAttribute = new StringAttribute(((MyConfig)Config).DatabaseCredentials.DefaultCharset, null) };
            }
            else if (strType.StringAttribute.CharacterSet == null)
            {
                strType = strType with { StringAttribute = strType.StringAttribute with { CharacterSet = ((MyConfig)Config).DatabaseCredentials.DefaultCharset } };
            }
            dataType = strType;
        }

        return new Cast(NormalizeExpression(cast.Expression, inSelect: inSelect, flatten: flatten), dataType);
    }
}
