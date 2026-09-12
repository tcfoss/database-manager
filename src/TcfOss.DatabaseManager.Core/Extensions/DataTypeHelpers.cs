using System.Globalization;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;

namespace TcfOss.DatabaseManager.Core.Extensions;

public static class DataTypeHelpers
{
    public static LiteralValue? ToLiteralValueExpression(this DateTime? dateTime)
    {
        if (dateTime == null)
        {
            return null;
        }

        return dateTime.Value.ToLiteralValueExpression();
    }

    public static LiteralValue ToLiteralValueExpression(this DateTime dateTime)
    {
        return new LiteralValue(new Value.SingleQuotedString(dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
    }
}
