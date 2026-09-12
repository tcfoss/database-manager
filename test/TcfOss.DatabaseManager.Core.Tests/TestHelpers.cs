using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Tests;

public static partial class TestHelpers
{
    private static readonly Regex s_whitespaceRegex = CreateWhitespaceRegex();

    public static string NormalizeWhitespace(this string given)
    {
        return s_whitespaceRegex.Replace(given, " ");
    }

    public static string N(this SqlValueList<Identifier> identifiers)
    {
        return string.Join(".", identifiers.Select(i => i.Name));
    }

    public static string N(this ItemRef itemRef)
    {
        return N(itemRef.Identifiers);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CreateWhitespaceRegex();
}
