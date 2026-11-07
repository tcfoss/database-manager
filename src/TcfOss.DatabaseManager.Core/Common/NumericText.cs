using System.Globalization;
using System.Numerics;

namespace TcfOss.DatabaseManager.Core.Common;

internal static class NumericText
{
    public static string ToDecimal(string text)
    {
        text = text.Replace("_", "", StringComparison.Ordinal);
        int offset = text.Length > 0 && text[0] is '+' or '-' ? 1 : 0;
        if (text.Length <= offset + 2 || text[offset] != '0')
        {
            return text;
        }
        int radix = text[offset + 1] switch
        {
            'x' or 'X' => 16,
            'o' or 'O' => 8,
            'b' or 'B' => 2,
            _ => 0
        };
        if (radix == 0)
        {
            return text;
        }
        BigInteger value = BigInteger.Zero;
        foreach (char character in text.AsSpan(offset + 2))
        {
            int digit = character switch
            {
                >= '0' and <= '9' => character - '0',
                >= 'a' and <= 'f' => character - 'a' + 10,
                >= 'A' and <= 'F' => character - 'A' + 10,
                _ => -1
            };
            if (digit < 0 || digit >= radix)
            {
                return text;
            }
            value = value * radix + digit;
        }
        if (offset != 0 && text[0] == '-')
        {
            value = -value;
        }
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
