using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace TcfOss.DatabaseManager.Core.Common;

internal static class PgStringDecoders
{
    private static readonly Encoding s_utf8 = new UTF8Encoding(false, true);

    internal static string DecodeEscapeString(string text)
    {
        try
        {
            return DecodeEscapeBytes(s_utf8.GetBytes(text));
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

    internal static string DecodeUnicodeString(string text, char escapeCharacter = '\\', char quoteCharacter = '\'')
    {
        if (escapeCharacter == '\0' || !char.IsAscii(escapeCharacter)
            || char.IsAsciiHexDigit(escapeCharacter) || char.IsWhiteSpace(escapeCharacter)
            || escapeCharacter is '+' or '\'' or '"')
        {
            throw new FormatException();
        }
        var decoded = new StringBuilder(text.Length);
        int position = 0;
        while (position < text.Length)
        {
            char current = text[position++];
            if (current == quoteCharacter)
            {
                if (position >= text.Length || text[position++] != quoteCharacter)
                {
                    throw new FormatException();
                }
                decoded.Append(current);
            }
            else if (current == escapeCharacter)
            {
                if (position < text.Length && text[position] == escapeCharacter)
                {
                    position++;
                    decoded.Append(current);
                    continue;
                }
                uint scalar = ReadUnicodeScalar(text, ref position);
                if (scalar is >= 0xD800 and <= 0xDBFF)
                {
                    if (position >= text.Length || text[position++] != escapeCharacter)
                    {
                        throw new FormatException();
                    }
                    scalar = CombineSurrogatePair(scalar, ReadUnicodeScalar(text, ref position));
                }
                decoded.Append(ToUnicodeScalar(scalar).ToString());
            }
            else if (char.IsHighSurrogate(current))
            {
                if (position >= text.Length || !char.IsLowSurrogate(text[position]))
                {
                    throw new FormatException();
                }
                decoded.Append(current).Append(text[position++]);
            }
            else if (current == '\0' || char.IsLowSurrogate(current))
            {
                throw new FormatException();
            }
            else
            {
                decoded.Append(current);
            }
        }
        return decoded.ToString();
    }

    private static string DecodeEscapeBytes(ReadOnlySpan<byte> source)
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
                byte hexadecimal = byte.Parse(source.Slice(position, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                position += digits;
                decoded.Add(hexadecimal);
            }
            else if (escaped is (byte)'u' or (byte)'U')
            {
                uint scalar = ReadEscapeUnicode(source, ref position, escaped);
                if (scalar is >= 0xD800 and <= 0xDBFF)
                {
                    if (position + 1 >= source.Length || source[position++] != '\\' || source[position] is not ((byte)'u' or (byte)'U'))
                    {
                        throw new FormatException();
                    }

                    byte marker = source[position];
                    position++;
                    scalar = CombineSurrogatePair(scalar, ReadEscapeUnicode(source, ref position, marker));
                }
                int written = ToUnicodeScalar(scalar).EncodeToUtf8(unicodeBytes);
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

    private static uint ReadEscapeUnicode(ReadOnlySpan<byte> source, ref int position, byte marker)
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

    private static uint ReadUnicodeScalar(string text, ref int position)
    {
        int digits = 4;
        if (position < text.Length && text[position] == '+')
        {
            position++;
            digits = 6;
        }
        if (text.Length - position < digits
            || !uint.TryParse(text.AsSpan(position, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint scalar))
        {
            throw new FormatException();
        }
        position += digits;
        return scalar;
    }

    private static uint CombineSurrogatePair(uint high, uint low)
    {
        if (low is < 0xDC00 or > 0xDFFF)
        {
            throw new FormatException();
        }
        return 0x10000 + (high - 0xD800) * 0x400 + low - 0xDC00;
    }

    private static Rune ToUnicodeScalar(uint scalar)
    {
        if (scalar == 0 || scalar > 0x10FFFF || !Rune.TryCreate((int)scalar, out Rune unicode))
        {
            throw new FormatException();
        }
        return unicode;
    }
}
