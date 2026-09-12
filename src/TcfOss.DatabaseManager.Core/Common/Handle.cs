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
        return new Handle(
            GetNamePart(identifier, nameHandling));
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
        return nameHandling switch
        {
            NameHandling.None => part.Name,
            NameHandling.Lowercase => part.Name.ToLowerInvariant(),
            NameHandling.LowercaseUnlessQuoted => part.QuoteStyle != QuoteStyle.None ? part.Name : part.Name.ToLowerInvariant(),
            NameHandling.Uppercase => part.Name.ToUpperInvariant(),
            NameHandling.UppercaseUnlessQuoted => part.QuoteStyle != QuoteStyle.None ? part.Name : part.Name.ToUpperInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(nameHandling), nameHandling, null)
        };
    }
}
