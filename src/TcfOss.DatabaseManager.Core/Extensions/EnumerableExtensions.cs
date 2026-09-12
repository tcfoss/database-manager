using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Extensions;

public static class EnumerableExtensions
{
    public static bool SafeAny<T>([NotNullWhen(true)] this IEnumerable<T>? enumerable)
    {
        return enumerable != null && enumerable.Any();
    }

    public static SqlValueList<T> ToSequence<T>(this IEnumerable<T> enumerable)
        where T : IEquatable<T>
    {
        return [.. enumerable];
    }

    public static SqlValueList<T> NonInert<T>(this IEnumerable<T> enumerable)
        where T : Statement, IEquatable<T>
    {
        return enumerable.Where(e => e is not InertOnly).ToSequence();
    }

    public static void ForEach<T>(this List<T> enumerable, Action<T, int> action)
    {
        for (int i = 0; i < enumerable.Count; i++)
        {
            action(enumerable[i], i);
        }
    }

    public static string ToIdentifier(this IEnumerable<string?>? parts)
    {
        return string.Join(".", parts?.Where(p => !string.IsNullOrEmpty(p)) ?? []);
    }
}
