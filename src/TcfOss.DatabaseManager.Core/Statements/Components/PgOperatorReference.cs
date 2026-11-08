using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgOperatorReference : IWriteSql
{
    private static readonly SearchValues<char> s_characters = SearchValues.Create("+-*/<>=~!@#%^&|`?");
    private static readonly SearchValues<char> s_nonStandardCharacters = SearchValues.Create("~!@#%^&|`?");

    public string Name { get; }
    public Identifier? Schema { get; init; }
    public bool UseOperatorSyntax { get; init; }

    public PgOperatorReference(string name)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException(null, nameof(name));
        }
        Name = name;
    }

    internal static bool IsValidName([NotNullWhen(true)] string? name)
    {
        return name is { Length: > 0 and <= 63 }
            && name.AsSpan().IndexOfAnyExcept(s_characters) < 0
            && !name.Contains("--", StringComparison.Ordinal)
            && !name.Contains("/*", StringComparison.Ordinal)
            && (name.Length == 1 || name[^1] is not ('+' or '-') || name.AsSpan().IndexOfAny(s_nonStandardCharacters) >= 0);
    }

    public void ToSql(SqlTextWriter writer)
    {
        if (UseOperatorSyntax || Schema != null)
        {
            writer.Write("OPERATOR(");
            if (Schema != null)
            {
                writer.WriteSql($"{Schema}.");
            }
            writer.Write(Name);
            writer.Write(")");
        }
        else
        {
            writer.Write(Name);
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        if (UseOperatorSyntax || Schema != null)
        {
            writer.Write("OPERATOR(");
            if (Schema != null)
            {
                Schema.FormatSql(writer, manager);
                writer.Write(".");
            }
            writer.Write(Name);
            writer.Write(")");
        }
        else
        {
            writer.Write(Name);
        }
    }
}
