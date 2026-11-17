using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class DuplicateGuard
{
    private readonly HashSet<string> _seen = [];

    public bool Add([NotNullWhen(false)] string? key)
    {
        if (key == null)
        {
            return true;
        }
        return _seen.Add(key);
    }

    public void AddGuarded(string? key, Location location)
    {
        if (!Add(key))
        {
            throw new ParseException.Duplicate(key, location);
        }
    }

    public void AddGuarded(IHaveDuplicateCheckKey item, Location location)
    {
        AddGuarded(item.DuplicateCheckKey, location);
    }
}
