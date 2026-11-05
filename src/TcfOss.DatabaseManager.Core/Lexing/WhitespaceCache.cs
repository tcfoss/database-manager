using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Lexing;

/// <summary>
/// Shared instances of common whitespace runs; the records are immutable, so sharing is safe.
/// </summary>
internal static class WhitespaceCache
{
    private const int MaxCachedLength = 32;

    private static readonly NonSql.Spaces[] s_spaces = Create(n => new NonSql.Spaces(n));
    private static readonly NonSql.Tabs[] s_tabs = Create(n => new NonSql.Tabs(n));
    private static readonly NonSql.Newlines[] s_newlines = Create(n => new NonSql.Newlines(n));

    public static NonSql.Spaces Spaces(int count)
    {
        return count <= MaxCachedLength ? s_spaces[count] : new NonSql.Spaces(count);
    }

    public static NonSql.Tabs Tabs(int count)
    {
        return count <= MaxCachedLength ? s_tabs[count] : new NonSql.Tabs(count);
    }

    public static NonSql.Newlines Newlines(int count)
    {
        return count <= MaxCachedLength ? s_newlines[count] : new NonSql.Newlines(count);
    }

    private static T[] Create<T>(Func<int, T> factory)
    {
        var result = new T[MaxCachedLength + 1];
        for (int i = 1; i <= MaxCachedLength; i++)
        {
            result[i] = factory(i);
        }
        return result;
    }
}
