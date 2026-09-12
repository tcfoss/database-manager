using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DefinitionMapping;

public static class MyObjectMapper
{
    public static List<ObjectKeyMapping<TObject>> GetMappings<TObject>(DatabaseObjectDict<TObject> start, DatabaseObjectDict<TObject> end, out HashSet<ObjectHandle> excluded, bool excludeEquals = true, HashSet<ObjectHandle>? neverExclude = null)
        where TObject : IEquatable<TObject>, IDatabaseObject
    {
        var mappings = new List<ObjectKeyMapping<TObject>>();
        neverExclude ??= [];
        excluded = [];

        foreach (ObjectHandle objKey in start.Keys.Union(end.Keys))
        {
            TObject? startObj = start.GetValueOrDefault(objKey);
            TObject? endObj = end.GetValueOrDefault(objKey);
            if (!excludeEquals || !(startObj?.Equals(endObj) ?? false) || neverExclude.Contains(objKey))
            {
                mappings.Add(new ObjectKeyMapping<TObject>(objKey, (startObj?.Name ?? endObj?.Name)!, startObj, endObj));
            }
            else
            {
                excluded.Add(objKey);
            }
        }
        return mappings;
    }

    public static List<ObjectKeyMapping<TObject>> GetMappings<TObject>(DatabaseObjectDict<TObject> start, DatabaseObjectDict<TObject> end, bool excludeEquals = true, HashSet<ObjectHandle>? neverExclude = null)
        where TObject : IEquatable<TObject>, IDatabaseObject
    {
        return GetMappings(start, end, out _, excludeEquals, neverExclude);
    }

    public static List<HandleMapping<T>> GetKeyMappings<T>(DatabaseComponentDict<T> start, DatabaseComponentDict<T> end) where T : IHaveOptionalIdentifierName, IEquatable<T>
    {
        HashSet<Handle> allNames = [];

        foreach (Handle startItem in start.Keys)
        {
            allNames.Add(startItem);
        }
        foreach (Handle endItem in end.Keys)
        {
            allNames.Add(endItem);
        }

        return [.. allNames.Select(key => new HandleMapping<T>(key, start.GetValueOrDefault(key), end.GetValueOrDefault(key))).OrderBy(x => x.Name.ToString())];
    }

    private static ColumnIdentifier? GetPreviousIdentifier(SqlValueList<MyColumn> columns, int startIndex, HashSet<string>? skipCols)
    {
        skipCols ??= [];

        for (int i = startIndex - 1; i >= 0; i--)
        {
            MyColumn curr = columns[i];
            if (!skipCols.Contains(curr.Name.Name))
            {
                return curr.Name;
            }
        }
        return null;
    }

    private static (HashSet<string> startNames, HashSet<string> endNames, Dictionary<string, IndexPair> nameToIndex) GetColumnNameSets(SqlValueList<MyColumn> start, SqlValueList<MyColumn> end)
    {
        HashSet<string> startNames = [];
        HashSet<string> endNames = [];
        Dictionary<string, IndexPair> nameToIndex = [];

        end.ForEach((x, i) =>
        {
            endNames.Add(x.Name.Name);
            nameToIndex[x.Name.Name] = new IndexPair { EndIndex = i };
        });

        start.ForEach((x, i) =>
        {
            startNames.Add(x.Name.Name);
            if (!nameToIndex.TryGetValue(x.Name.Name, out IndexPair? value))
            {
                nameToIndex[x.Name.Name] = new IndexPair { StartIndex = i };
            }
            else
            {
                value.StartIndex = i;
            }
        });

        return (startNames, endNames, nameToIndex);
    }

    private static (Dictionary<string, int> startWithoutDropped, Dictionary<string, int> endWithoutAdded) GetIndexMapsWithoutAddsAndDrops(SqlValueList<MyColumn> start, HashSet<string> startNames, SqlValueList<MyColumn> end, HashSet<string> endNames)
    {
        var addedCols = endNames.Except(startNames).ToHashSet();
        var droppedCols = startNames.Except(endNames).ToHashSet();

        int i = 0;
        var startWithoutDropped = new Dictionary<string, int>();
        foreach (MyColumn startCol in start)
        {
            if (!droppedCols.Contains(startCol.Name.Name))
            {
                startWithoutDropped[startCol.Name.Name] = i;
                i++;
            }
        }

        i = 0;
        var endWithoutAdded = new Dictionary<string, int>();
        foreach (MyColumn endCol in end)
        {
            if (!addedCols.Contains(endCol.Name.Name))
            {
                endWithoutAdded[endCol.Name.Name] = i;
                i++;
            }
        }

        return (startWithoutDropped, endWithoutAdded);
    }

    public static List<ColumnMapping> GetColumnMappingSets(SqlValueList<MyColumn> start, SqlValueList<MyColumn> end)
    {
        (HashSet<string> startNames, HashSet<string> endNames, Dictionary<string, IndexPair> nameToIndex) = GetColumnNameSets(start, end);

        (Dictionary<string, int> startWithoutDropped, Dictionary<string, int> endWithoutAdded) = GetIndexMapsWithoutAddsAndDrops(start, startNames, end, endNames);

        var maps = new List<ColumnMapping>();
        foreach ((int? startIndex, int? endIndex) in nameToIndex.Values)
        {
            MyColumn? startCol = startIndex.HasValue ? start[startIndex.Value] : null;
            MyColumn? endCol = endIndex.HasValue ? end[endIndex.Value] : null;
            ColumnIdentifier? endPrev = endIndex.HasValue ? GetPreviousIdentifier(end, endIndex.Value, null) : null;

            if (startCol != null && endCol == null)
            {
                maps.Add(new(startCol, null, null, ColumnChangeType.Dropped, false));
                continue;
            }
            else if (startCol == null && endCol != null)
            {
                maps.Add(new(null, endCol, endPrev?.Name, ColumnChangeType.Added, false));
                continue;
            }

            bool moved = false;
            ColumnChangeType changeType = ColumnChangeType.NoChange;
            if (startWithoutDropped[startCol!.Name.Name] != endWithoutAdded[endCol!.Name.Name])
            {
                moved = true;
            }
            if (!startCol.Equals(endCol))
            {
                changeType = ColumnChangeType.Modified;
            }
            if (moved || changeType != ColumnChangeType.NoChange)
            {
                maps.Add(new(startCol, endCol, endPrev?.Name, changeType, moved));
            }
        }

        return maps;
    }

    private sealed class IndexPair
    {
        public int? StartIndex { get; set; }
        public int? EndIndex { get; init; }

        public void Deconstruct(out int? startIndex, out int? endIndex)
        {
            startIndex = StartIndex;
            endIndex = EndIndex;
        }
    }

    public enum ColumnChangeType
    {
        NoChange,
        Added,
        Dropped,
        Modified,
    }

    public record struct ColumnMapping(MyColumn? Start, MyColumn? End, string? EndPrevious, ColumnChangeType ChangeType, bool Moved);
}
