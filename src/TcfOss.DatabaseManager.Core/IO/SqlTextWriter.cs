using System.Runtime.CompilerServices;
using System.Text;
using TcfOss.DatabaseManager.Core.Formatting;

namespace TcfOss.DatabaseManager.Core.IO;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CA1305 // Specify IFormatProvider

public class SqlTextWriter(StringBuilder stringBuilder) : StringWriter(stringBuilder)
{
    public Indenter Indenter => field ??= new Indenter();

    public SqlTextWriter(StringBuilder stringBuilder, Indenter indenter) : this(stringBuilder)
    {
        Indenter = indenter;
    }

    // ReSharper disable once UnusedParameter.Global
    public void WriteSql([InterpolatedStringHandlerArgument("")] ref SqlInterpolatedStringHandler handler)
    {
        // no implementation needed; handled by the interpolation handler with
        // "this" passed in as the "" SqlTextWriter constructor parameter argument
    }

    // ReSharper disable once UnusedParameter.Global
    public void WriteSqlI([InterpolatedStringHandlerArgument("")] ref IndentedSqlInterpolatedStringHandler handler)
    {
        // no implementation needed; handled by the interpolation handler with
        // "this" passed in as the "" SqlTextWriter constructor parameter argument
    }

    public void WriteSqlI(string? value)
    {
        WriteSqlI($"{value}");
    }

    public void RemoveChar(char c)
    {
        StringBuilder sb = GetStringBuilder();
        if (sb.Length == 0)
        {
            return;
        }

        int startChar = sb.Length - 1;
        while (startChar >= 0 && char.IsWhiteSpace(sb[startChar]))
        {
            startChar--;
        }
        if (sb[startChar] == c)
        {
            sb.Length = startChar;
        }
    }

    public void TrimEnd()
    {
        StringBuilder sb = GetStringBuilder();
        int endChar = sb.Length - 1;
        while (endChar >= 0 && char.IsWhiteSpace(sb[endChar]))
        {
            endChar--;
        }
        sb.Length = endChar + 1;
    }

    public bool EndWithNewLine()
    {
        StringBuilder sb = GetStringBuilder();
        return sb.Length > 0 && sb[^1] == '\n';
    }

    public int GetCurrentLineLength()
    {
        StringBuilder sb = GetStringBuilder();
        int i = sb.Length - 1;
        while (i >= 0 && sb[i] != '\n')
        {
            i--;
        }
        return sb.Length - 1 - i;
    }

    public void WriteDelimited<T>(IEnumerable<T>? enumerable, string delimiter = ", ") where T : IWriteSql
    {
        if (enumerable == null)
        {
            return;
        }

        T[] components = enumerable as T[] ?? [.. enumerable];

        for (int i = 0; i < components.Length; i++)
        {
            if (i > 0)
            {
                Write(delimiter);
            }

            components[i].ToSql(this);
        }
    }

    public void FormatDelimited<T>(IEnumerable<T>? enumerable, FormatManager manager, string delimiter = ", ") where T : IWriteSql
    {
        if (enumerable == null)
        {
            return;
        }

        T[] components = enumerable as T[] ?? [.. enumerable];

        for (int i = 0; i < components.Length; i++)
        {
            if (i > 0)
            {
                Write(delimiter);
            }

            components[i].FormatSql(this, manager);
        }
    }

    public void FormatDelimitedLines<T>(IEnumerable<T>? enumerable, FormatManager manager, string delimiter = ",") where T : IWriteSql
    {
        if (enumerable == null)
        {
            return;
        }

        T[] components = enumerable as T[] ?? [.. enumerable];

        for (int i = 0; i < components.Length; i++)
        {
            components[i].FormatSql(this, manager);
            if (i < components.Length - 1)
            {
                WriteLine(delimiter);
            }
        }
    }

    public void WriteTerminated<T>(IEnumerable<T> enumerable, string terminator = "; ") where T : IWriteSql
    {
        foreach (T el in enumerable)
        {
            el.ToSql(this);
            Write(terminator);
        }
    }
}
