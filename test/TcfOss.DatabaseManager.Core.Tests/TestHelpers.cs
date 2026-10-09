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

    public static void AssertEqualItemNames(this IReadOnlyCollection<ItemRef> items, params string[] expectedNames)
    {
        Assert.Collection(items,
            [.. expectedNames.Select(name => new Action<ItemRef>(item => Assert.Equal(name, item.N())))]);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CreateWhitespaceRegex();
}
