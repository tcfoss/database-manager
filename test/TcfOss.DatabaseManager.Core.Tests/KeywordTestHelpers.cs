using System.Collections;
using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Tests;

public abstract class ExhaustiveKeywordTestData<T> : IEnumerable<TheoryDataRow<Keyword, T>>
{
    protected readonly  HashSet<Keyword> SupportedKeywords = [];
    protected abstract IEnumerable<TheoryDataRow<Keyword, T>> GetSupportedData();

    protected abstract T GetDefaultValue();

    public virtual IEnumerator<TheoryDataRow<Keyword, T>> GetEnumerator()
    {
        foreach (var data in GetSupportedData())
        {
            SupportedKeywords.Add(data.Data.Item1);
            yield return data;
        }

        foreach (var keyword in Enum.GetValues<Keyword>().Cast<Keyword>())
        {
            if (!SupportedKeywords.Contains(keyword))
            {
                yield return new TheoryDataRow<Keyword, T>(keyword, GetDefaultValue());
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}