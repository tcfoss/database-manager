using System.Collections.Immutable;
using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Postgres.BuiltIn;

public class PgFunctionNameProvider : IFunctionNameProvider
{
    private static readonly FunctionNameProvider s_sharedProvider = new();
    private static readonly ImmutableHashSet<string> s_functionsWithoutParentheses = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "LOCALTIME", "LOCALTIMESTAMP",
        "CURRENT_ROLE", "CURRENT_USER", "SESSION_USER", "SYSTEM_USER", "USER",
        "CURRENT_CATALOG", "CURRENT_SCHEMA");

    public bool IsBuiltInFunction(string name)
    {
        return IsKeywordFunction(name) || s_sharedProvider.IsBuiltInFunction(name);
    }

    public bool IsReservedKeyword(string name)
    {
        return IsKeywordFunction(name) || s_sharedProvider.IsReservedKeyword(name);
    }

    public static bool AllowsFunctionCallWithoutParentheses(string name)
    {
        return s_functionsWithoutParentheses.Contains(name);
    }

    public static bool IsKeywordFunction(string name)
    {
        return AllowsFunctionCallWithoutParentheses(name)
            || name.ToUpperInvariant() is "COALESCE" or "GREATEST" or "LEAST" or "NULLIF";
    }
}
