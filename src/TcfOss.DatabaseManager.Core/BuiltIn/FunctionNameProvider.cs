using System.Collections.Immutable;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

public class FunctionNameProvider : IFunctionNameProvider
{
    private static readonly ImmutableHashSet<string> s_functionNames = [
        "CURRENT_DATE",
        "CURRENT_TIME",
        "CURRENT_TIMESTAMP",
        "NOW",
        "CURRENT_USER",
        "SYSTEM_USER",

        /* aggregate functions */
        "AVG",
        "COUNT",
        "MIN",
        "MAX",
        "SUM",

        "COALESCE",
        "NULLIF",

        "LOWER",
        "UPPER",
        "TRIM",
        "SUBSTRING"
    ];

    private static readonly ImmutableHashSet<string> s_functionsAllowedWithoutParentheses = [
        "CURRENT_DATE",
        "CURRENT_TIME",
        "CURRENT_TIMESTAMP",
        "NOW",
        "CURRENT_USER",
        "SYSTEM_USER",
    ];

    public bool IsBuiltInFunction(string name)
    {
        return s_functionNames.Contains(name.ToUpperInvariant());
    }

    public bool IsReservedKeyword(string name)
    {
        return false;
    }

    public static bool AllowsFunctionCallWithoutParentheses(string name)
    {
        return s_functionsAllowedWithoutParentheses.Contains(name.ToUpperInvariant());
    }
}
