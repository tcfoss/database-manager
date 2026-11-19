using System.Globalization;
using System.Numerics;

namespace TcfOss.DatabaseManager.Core.Common;

internal static class NumericText
{
    public static string NormalizeDecimal(string text)
    {
        int exponentStart = text.IndexOfAny(['e', 'E']);
        string mantissa = exponentStart < 0 ? text : text[..exponentStart];
        string sign = mantissa.StartsWith('-') ? "-" : "";
        mantissa = mantissa.TrimStart('+', '-').TrimStart('0');
        if (mantissa.Length == 0 || mantissa[0] == '.')
        {
            mantissa = "0" + mantissa;
        }
        string exponent = "";
        if (exponentStart >= 0)
        {
            string digits = text[(exponentStart + 1)..];
            bool negative = digits.StartsWith('-');
            digits = digits.TrimStart('+', '-').TrimStart('0');
            exponent = "e" + (digits.Length == 0 ? "0" : (negative ? "-" : "") + digits);
        }
        // Decimal points, fractional zeros, and exponent forms carry type/scale information.
        return sign + mantissa + exponent;
    }

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
