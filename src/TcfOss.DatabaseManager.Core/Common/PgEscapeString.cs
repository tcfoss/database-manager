using System.Buffers.Text;
using System.Text;

namespace TcfOss.DatabaseManager.Core.Common;

internal static class PgEscapeString
{
    private static readonly Encoding s_utf8 = new UTF8Encoding(false, true);

    internal static string Decode(string text)
    {
        try
        {
            return DecodeBytes(s_utf8.GetBytes(text));
        }
        catch (EncoderFallbackException exception)
        {
            throw new FormatException(null, exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new FormatException(null, exception);
        }
    }

    private static string DecodeBytes(ReadOnlySpan<byte> source)
    {
        var decoded = new List<byte>(source.Length);
        Span<byte> unicodeBytes = stackalloc byte[4];
        int position = 0;
        while (position < source.Length)
        {
            byte current = source[position++];
            if (current == '\'')
            {
                if (position >= source.Length || source[position++] != '\'')
                {
                    throw new FormatException();
                }
                decoded.Add(current);
                continue;
            }
            if (current != '\\')
            {
                decoded.Add(current);
                continue;
            }
            if (position >= source.Length)
            {
                throw new FormatException();
            }

            byte escaped = source[position++];
            if (escaped is >= (byte)'0' and <= (byte)'7')
            {
                int octal = escaped - '0';
                int digits = 1;
                while (digits < 3 && position < source.Length && source[position] is >= (byte)'0' and <= (byte)'7')
                {
                    octal = octal * 8 + source[position++] - '0';
                    digits++;
                }
                decoded.Add(unchecked((byte)octal));
            }
            else if (escaped == 'x' && position < source.Length && char.IsAsciiHexDigit((char)source[position]))
            {
                int digits = position + 1 < source.Length && char.IsAsciiHexDigit((char)source[position + 1]) ? 2 : 1;
                if (!Utf8Parser.TryParse(source.Slice(position, digits), out byte hexadecimal, out int consumed, 'x') || consumed != digits)
                {
                    throw new FormatException();
                }
                position += digits;
                decoded.Add(hexadecimal);
            }
            else if (escaped is (byte)'u' or (byte)'U')
            {
                uint scalar = ReadUnicode(source, ref position, escaped);
                if (scalar is >= 0xD800 and <= 0xDBFF)
                {
                    if (position + 1 >= source.Length || source[position++] != '\\' || source[position] is not ((byte)'u' or (byte)'U'))
                    {
                        throw new FormatException();
                    }
                    uint low = ReadUnicode(source, ref position, source[position++]);
                    if (low is < 0xDC00 or > 0xDFFF)
                    {
                        throw new FormatException();
                    }
                    scalar = 0x10000 + (scalar - 0xD800) * 0x400 + low - 0xDC00;
                }
                if (scalar > 0x10FFFF || !Rune.TryCreate((int)scalar, out Rune unicode))
                {
                    throw new FormatException();
                }
                int written = unicode.EncodeToUtf8(unicodeBytes);
                foreach (byte value in unicodeBytes[..written])
                {
                    decoded.Add(value);
                }
            }
            else
            {
                decoded.Add(escaped switch
                {
                    (byte)'b' => (byte)'\b',
                    (byte)'f' => (byte)'\f',
                    (byte)'n' => (byte)'\n',
                    (byte)'r' => (byte)'\r',
                    (byte)'t' => (byte)'\t',
                    _ => escaped
                });
            }
        }
        if (decoded.Contains(0))
        {
            throw new FormatException();
        }
        return s_utf8.GetString([.. decoded]);
    }

    private static uint ReadUnicode(ReadOnlySpan<byte> source, ref int position, byte marker)
    {
        int digits = marker == 'u' ? 4 : 8;
        if (source.Length - position < digits
            || !Utf8Parser.TryParse(source.Slice(position, digits), out uint scalar, out int consumed, 'x')
            || consumed != digits)
        {
            throw new FormatException();
        }
        position += digits;
        return scalar;
    }
}
