using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

/// <summary>
/// Container for meta-information about a statement.
///
/// To avoid throwing off value equality for the statements themselves,
/// every (non-null) MetaData is equal to every other (non-null) MetaData.
/// </summary>
public sealed class MetaData : IEquatable<MetaData>
{
    public List<NonSql>? PreNonSql { get; set; }
    public List<NonSql>? PostNonSql { get; set; }
    public int? Start { get; set; }
    public int? End { get; set; }
    public int? EndPlusTerminatorLength { get; set; }
    public string? RawText { get; set; }

    public bool Equals(MetaData? other)
    {
        return other is not null;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as MetaData);
    }

    public override int GetHashCode()
    {
        return 42;
    }

    public void FormatPreNonSql(SqlTextWriter writer, FormatManager manager)
    {
        if (manager.ExpectingNewLineBeforeStatement)
        {
            // NonSql.LineComment always end with a new line, so it counts.
            bool containsNewlines = PreNonSql?.Any(
                x => x is NonSql.Newlines || x is NonSql.LineComment) ?? false;
            if (!containsNewlines)
            {
                writer.WriteLine();
            }
            manager.ExpectingNewLineBeforeStatement = false;
        }

        FormatNonSqlList(PreNonSql, writer, manager);
    }

    public void FormatPostNonSql(SqlTextWriter writer, FormatManager manager)
    {
        FormatNonSqlList(PostNonSql, writer, manager);
    }

    private static void FormatNonSqlList(List<NonSql>? nonSqlList, SqlTextWriter writer, FormatManager manager)
    {
        if (!nonSqlList.SafeAny())
        {
            return;
        }
        if (nonSqlList.Any(x => x is NonSql.Comment))
        {
            writer.TrimEnd();
        }
        int spacesPerTab = manager.Formatting.TabSize;
        for (int i = 0; i < nonSqlList.Count; i++)
        {
            NonSql nonSql = nonSqlList[i];
            if (nonSql is NonSql.BlockComment blockComment)
            {
                int originalSpaces = CountSpacesBefore(nonSqlList, i, spacesPerTab);
                blockComment.FormatSql(writer, manager, originalSpaces);
            }
            else
            {
                nonSql.FormatSql(writer, manager);
            }
        }
    }

    private static int CountSpacesBefore(List<NonSql> components, int endIndex, int spacesPerTab)
    {
        int startIndex = endIndex - 1;
        while (startIndex >= 0)
        {
            if (components[startIndex] is NonSql.Spaces || components[startIndex] is NonSql.Tabs)
            {
                startIndex--;
            }
            else
            {
                break;
            }
        }

        int spaceCount = 0;
        for (int i = startIndex + 1; i < endIndex; i++)
        {
            if (components[i] is NonSql.Spaces s)
            {
                spaceCount += s.Number;
            }
            else if (components[i] is NonSql.Tabs t)
            {
                int tabCount = t.Number;
                if (spaceCount % spacesPerTab != 0)
                {
                    spaceCount += spacesPerTab - (spaceCount % spacesPerTab);
                    tabCount--;
                }

                spaceCount += spacesPerTab * tabCount;
            }
        }

        return spaceCount;
    }
}
