using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace TcfOss.DatabaseManager.Core.Common;

[DebuggerDisplay("{NameScope}.{Name}")]
public readonly record struct Handle
    : IComparable<Handle>
{
    public string Name { get; }
    public string NameScope { get; }

    [JsonConstructor]
    private Handle(string name, string nameScope = "")
    {
        Name = name;
        NameScope = nameScope;
    }

    public int CompareTo(Handle other)
    {
        int namespaceComparison = string.Compare(NameScope, other.NameScope, StringComparison.Ordinal);
        if (namespaceComparison != 0)
        {
            return namespaceComparison;
        }

        return string.Compare(Name, other.Name, StringComparison.Ordinal);
    }

    public static Handle Create(Identifier identifier, Identifier nameScope, NameHandling nameHandling)
    {
        return new Handle(
            GetNamePart(identifier, nameHandling),
            GetNamePart(nameScope, nameHandling));
    }

    public static Handle Create(Identifier identifier, NameHandling nameHandling)
    {
        return new Handle(GetNamePart(identifier, nameHandling));
    }

    public static bool operator <(Handle left, Handle right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Handle left, Handle right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Handle left, Handle right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Handle left, Handle right)
    {
        return left.CompareTo(right) >= 0;
    }

    public static string GetNamePart(Identifier part, NameHandling nameHandling)
    {
        return GetNamePart(part.Name, part.QuoteStyle, nameHandling);
    }

    public static string GetNamePart(string name, QuoteStyle quoteStyle, NameHandling nameHandling)
    {
        return nameHandling switch
        {
            NameHandling.None => name,
            NameHandling.Lowercase => name.ToLowerInvariant(),
            NameHandling.LowercaseUnlessQuoted => quoteStyle != QuoteStyle.None ? name : name.ToLowerInvariant(),
            NameHandling.Uppercase => name.ToUpperInvariant(),
            NameHandling.UppercaseUnlessQuoted => quoteStyle != QuoteStyle.None ? name : name.ToUpperInvariant(),
            NameHandling.Postgres => GetPostgresNamePart(name, quoteStyle),
            _ => throw new ArgumentOutOfRangeException(nameof(nameHandling), nameHandling, null)
        };
    }

    private static string GetPostgresNamePart(string name, QuoteStyle quoteStyle)
    {
        const int maxBytes = 63;
        Span<char> buffer = stackalloc char[Math.Min(name.Length, maxBytes)];
        int byteCount = 0;
        int charCount = 0;
        foreach (Rune rune in name.EnumerateRunes())
        {
            if (byteCount + rune.Utf8SequenceLength > maxBytes)
            {
                break;
            }
            Rune stored = quoteStyle == QuoteStyle.None && rune.Value is >= 'A' and <= 'Z'
                ? new Rune(rune.Value + ('a' - 'A'))
                : rune;
            charCount += stored.EncodeToUtf16(buffer[charCount..]);
            byteCount += rune.Utf8SequenceLength;
        }
        ReadOnlySpan<char> result = buffer[..charCount];
        return result.SequenceEqual(name.AsSpan()) ? name : new string(result);
    }

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
    public static bool RequiresQuotingForCase(Identifier part, NameHandling nameHandling)
    {
        return nameHandling switch
        {
            NameHandling.None => false,
            NameHandling.Lowercase => false,
            NameHandling.LowercaseUnlessQuoted => part.Name != part.Name.ToLowerInvariant(),
            NameHandling.Uppercase => false,
            NameHandling.UppercaseUnlessQuoted => part.Name != part.Name.ToUpperInvariant(),
            NameHandling.Postgres => part.Name.AsSpan().ContainsAnyInRange('A', 'Z'),
            _ => throw new ArgumentOutOfRangeException(nameof(nameHandling), nameHandling, null)
        };
    }
}
