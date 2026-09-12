using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.MySql.StatementAnalysis;

public class MyComponentNormalizer(QuoteStyle quoteStyle, IFunctionNameProvider functionNameProvider) : ComponentNormalizer(quoteStyle, functionNameProvider)
{
    public override Value NormalizeValue(Value value)
    {
        if (value is Value.Boolean boolValue)
        {
            if (boolValue.Value)
            {
                return new Value.Number("1", false);
            }
            else
            {
                return new Value.Number("0", false);
            }
        }
        return value;
    }
}
