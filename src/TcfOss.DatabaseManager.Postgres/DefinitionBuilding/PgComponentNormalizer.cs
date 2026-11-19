using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Postgres.Configuration;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgComponentNormalizer(PgConfig config)
    : ComponentNormalizer(config.QuoteStyle, new FunctionNameProvider(), config.NameHandling)
{
    public override Value NormalizeValue(Value value)
    {
        if (value is not Value.Number { IsLong: false } number)
        {
            return base.NormalizeValue(value);
        }

        Value.Number decimalNumber = number.DecimalText == number.Value
            ? number : new Value.Number(number.DecimalText, false);
        return NormalizeDecimalNumber(decimalNumber);
    }
}
