using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.StatementAnalysis;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyNormalizer(MyConfig config, MyComponentNormalizer componentNormalizer)
    : Normalizer(config, componentNormalizer)
{
    protected override INormalizeSql ConstructNewNormalizer()
    {
        return new MyNormalizer((MyConfig)Config, (MyComponentNormalizer)ComponentNormalizer);
    }

    protected override Expression NormalizeCast(Cast cast, bool inSelect = false, bool flatten = false)
    {
        DataType dataType = cast.DataType;
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
