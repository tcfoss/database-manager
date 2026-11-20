using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Postgres.BuiltIn;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgExpressionNormalizer(ConfigBase config, ComponentNormalizer componentNormalizer)
    : Normalizer(config, componentNormalizer)
{
    protected override INormalizeSql ConstructNewNormalizer()
    {
        return new PgExpressionNormalizer(Config, ComponentNormalizer);
    }

    protected override FunctionCall NormalizeFunctionCall(FunctionCall functionCall, bool inSelect = false, bool flatten = false)
    {
        FunctionCall normalized = base.NormalizeFunctionCall(functionCall, inSelect, flatten);
        if (!flatten)
        {
            return normalized;
        }
        ObjectName name = ComponentNormalizer.NormalizeObjectName(functionCall.Name);
        if (functionCall.Name.Values.Count == 1 && PgFunctionNameProvider.IsKeywordFunction(name.Values[0].Name))
        {
            name = new ObjectName(name.Values[0] with
            {
                QuoteStyle = functionCall.Name.Values[0].QuoteStyle == QuoteStyle.None ? QuoteStyle.None : QuoteStyle.Ansi
            });
        }
        return normalized with
        {
            Name = name
        };
    }
}
