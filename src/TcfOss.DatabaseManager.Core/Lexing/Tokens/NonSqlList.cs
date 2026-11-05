using System.Collections;
using NonSqlComponent = TcfOss.DatabaseManager.Core.Statements.Components.NonSql;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

/// <summary>
/// Contiguous non-SQL components preceding a token, along with the source span they cover.
/// </summary>
/// <remarks>
public sealed class NonSqlList : IReadOnlyList<NonSqlComponent>
{
    public List<NonSqlComponent> Items { get; } = [];
    public required int Start { get; init; }
    public int End { get; set; }
    public int Length => End - Start;

    public int Count => Items.Count;

    public NonSqlComponent this[int index] => Items[index];

    public void Add(NonSqlComponent item)
    {
        Items.Add(item);
    }

    /// <summary>
    /// Append <paramref name="other"/>, merging whitespace runs of the same kind at the boundary.
    /// </summary>
    public void Append(NonSqlList other)
    {
        int skip = 0;
        if (Items.Count > 0 && other.Count > 0
            && Items[^1] is NonSqlComponent.Whitespace last
            && other[0] is NonSqlComponent.Whitespace first
            && last.GetType() == first.GetType())
        {
            Items[^1] = last with { Number = last.Number + first.Number };
            skip = 1;
        }

        for (int i = skip; i < other.Count; i++)
        {
            Items.Add(other[i]);
        }
        End = other.End;
    }

    public List<NonSqlComponent>.Enumerator GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator<NonSqlComponent> IEnumerable<NonSqlComponent>.GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Items.GetEnumerator();
    }
}
