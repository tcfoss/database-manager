using System.Text;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects;

internal static class StringLiteralEncodingHelpers
{
    /// <summary>
    /// Replace escape sequences in a string with their actual characters
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string Decode(string text)
    {
        var result = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            if (current == '\'' && i + 1 < text.Length && text[i + 1] == '\'')
            {
                i++;
                result.Append('\'');
            }
            else if (current == '\\' && i + 1 < text.Length)
            {
                char escaped = text[++i];
                if (escaped is '%' or '_')
                {
                    result.Append('\\');
                }
                result.Append(escaped switch
                {
                    '0' => '\0',
                    'b' => '\b',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'Z' => '\u001a',
                    _ => escaped
                });
            }
            else
            {
                result.Append(current);
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Replace control characters in a string with their escape sequences
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string Encode(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            result.Append(character switch
            {
                '\0' => "\\0",
                '\b' => "\\b",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\u001a' => "\\Z",
                '\\' => "\\\\",
                '\'' => "\\'",
                _ => character.ToString()
            });
        }
        return result.ToString();
    }
}
