using System.Collections.ObjectModel;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

public static class KeywordHelper
{
    private static readonly ReadOnlyDictionary<string, Keyword> s_lookup;

    static KeywordHelper()
    {
        var lookup = new Dictionary<string, Keyword>();
        foreach (Keyword enumVal in Enum.GetValues<Keyword>())
        {
            string? name = Enum.GetName(enumVal);
            if (name != "undefined")
            {
                lookup[name!] = enumVal;
            }
        }
        s_lookup = new ReadOnlyDictionary<string, Keyword>(lookup);
    }

    /// <summary>
    /// Return the Keyword enumeration corresponding to the given text, or `null`
    /// if the text is not a keyword. This function always capitalizes its input.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static Keyword GetKeyword(string text)
    {
        return s_lookup.GetValueOrDefault(text.ToUpperInvariant(), Keyword.undefined);
    }

    /// These keywords can't be used as a table alias, so that `FROM table_name alias`
    /// can be parsed unambiguously without looking ahead.
    private static readonly HashSet<Keyword> s_reservedForColumnAlias = [
        // Reserved as both a table and a column alias:
        Keyword.WITH,
        Keyword.EXPLAIN,
        Keyword.ANALYZE,
        Keyword.SELECT,
        Keyword.WHERE,
        Keyword.GROUP,
        Keyword.SORT,
        Keyword.HAVING,
        Keyword.ORDER,
        Keyword.TOP,
        Keyword.LATERAL,
        Keyword.VIEW,
        Keyword.LIMIT,
        Keyword.OFFSET,
        Keyword.FETCH,
        Keyword.UNION,
        Keyword.EXCEPT,
        Keyword.INTERSECT,
        Keyword.CLUSTER,
        Keyword.DISTRIBUTE,
        Keyword.RETURNING,
        // Reserved only as a column alias in the `SELECT` clause
        Keyword.FROM,
        Keyword.INTO,
        Keyword.END,
    ];

    public static bool IsReservedForColumnAlias(Keyword? keyword)
    {
        if (keyword is null)
        {
            return false;
        }
        return s_reservedForColumnAlias.Contains(keyword.Value);
    }

    /// Can't be used as a column alias, so that `SELECT Expression alias`
    /// can be parsed unambiguously without looking ahead.
    private static readonly HashSet<Keyword> s_reservedForTableAlias = [
        // Reserved as both a table and a column alias:
        Keyword.WITH,
        Keyword.EXPLAIN,
        Keyword.ANALYZE,
        Keyword.SELECT,
        Keyword.WHERE,
        Keyword.GROUP,
        Keyword.SORT,
        Keyword.HAVING,
        Keyword.ORDER,
        Keyword.PIVOT,
        Keyword.UNPIVOT,
        Keyword.TOP,
        Keyword.LATERAL,
        Keyword.VIEW,
        Keyword.LIMIT,
        Keyword.OFFSET,
        Keyword.FETCH,
        Keyword.UNION,
        Keyword.EXCEPT,
        Keyword.INTERSECT,
        // Reserved only as a table alias in the `FROM`/`JOIN` clauses:
        Keyword.ON,
        Keyword.JOIN,
        Keyword.INNER,
        Keyword.CROSS,
        Keyword.FULL,
        Keyword.LEFT,
        Keyword.RIGHT,
        Keyword.NATURAL,
        Keyword.USING,
        Keyword.CLUSTER,
        Keyword.DISTRIBUTE,
        Keyword.GLOBAL,
        Keyword.INTO,
        // for MSSQL-specific OUTER APPLY (seems reserved in most dialects)
        Keyword.OUTER,
        Keyword.SET,
        Keyword.QUALIFY,
        Keyword.WINDOW,
        Keyword.END,
        Keyword.FOR,
        // for MYSQL PARTITION SELECTION
        Keyword.PARTITION,
        Keyword.PREWHERE,
        Keyword.SETTINGS,
        Keyword.FORMAT,
        // for Snowflake START WITH .. CONNECT BY
        Keyword.START,
        Keyword.CONNECT,
        Keyword.AS, // TODO remove?
        // Reserved for snowflake MATCH_RECOGNIZE
        Keyword.MATCH_RECOGNIZE,
    ];

    public static bool IsReservedForTableAlias(Keyword? keyword)
    {
        if (keyword is null)
        {
            return false;
        }
        return s_reservedForTableAlias.Contains(keyword.Value);
    }
}
