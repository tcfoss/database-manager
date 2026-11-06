using System.Diagnostics;
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
            _ => throw new ArgumentOutOfRangeException(nameof(nameHandling), nameHandling, null)
        };
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
            _ => throw new ArgumentOutOfRangeException(nameof(nameHandling), nameHandling, null)
        };
    }
}
