using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Extensions;

public static class DictionaryExtensions
{
    public static List<T> GetObjects<T>(this DatabaseObjectDict<T> dict, string name)
        where T : IEquatable<T>
    {
        var possibleMatches = new List<T>();
        foreach (KeyValuePair<ObjectHandle, T> kvp in dict)
        {
            if (kvp.Key.Name.Equals(name, StringComparison.Ordinal))
            {
                possibleMatches.Add(kvp.Value);
            }
        }
        return possibleMatches;
    }

    public static T GetObject<T>(this DatabaseObjectDict<T> dict, string name)
        where T : IEquatable<T>
    {
        List<T> possibleMatches = GetObjects(dict, name);
        if (possibleMatches.Count == 0)
        {
            throw new KeyNotFoundException($"Object '{name}' not found in dictionary.");
        }
        if (possibleMatches.Count > 1)
        {
            throw new InvalidOperationException($"Multiple objects named '{name}' found in dictionary.");
        }
        return possibleMatches[0];
    }

    public static List<T> GetObjects<T>(this DatabaseObjectDict<T> dict, string name, string schema)
        where T : IEquatable<T>
    {
        var possibleMatches = new List<T>();
        foreach (KeyValuePair<ObjectHandle, T> kvp in dict)
        {
            if (kvp.Key.Name.Equals(name, StringComparison.Ordinal) && kvp.Key.Schema.Equals(schema, StringComparison.Ordinal))
            {
                possibleMatches.Add(kvp.Value);
            }
        }
        return possibleMatches;
    }

    public static T GetObject<T>(this DatabaseObjectDict<T> dict, string name, string schema)
        where T : IEquatable<T>
    {
        List<T> possibleMatches = GetObjects(dict, name, schema);
        if (possibleMatches.Count == 0)
        {
            throw new KeyNotFoundException($"Object '{schema}.{name}' not found in dictionary.");
        }
        if (possibleMatches.Count > 1)
        {
            throw new InvalidOperationException($"Multiple objects named '{schema}.{name}' found in dictionary.");
        }
        return possibleMatches[0];
    }

    public static T GetColumn<T>(this IDictionary<ColumnIdentifier, T> dict, string columnName)
    {
        foreach (KeyValuePair<ColumnIdentifier, T> kvp in dict)
        {
            if (kvp.Key.Name.Equals(columnName, StringComparison.Ordinal))
            {
                return kvp.Value;
            }
        }
        throw new KeyNotFoundException($"Column '{columnName}' not found in dictionary.");
    }

    public static T GetItem<T>(this IDictionary<Identifier, T> dict, string name)
    {
        foreach (KeyValuePair<Identifier, T> kvp in dict)
        {
            if (kvp.Key.Name.Equals(name, StringComparison.Ordinal))
            {
                return kvp.Value;
            }
        }
        throw new KeyNotFoundException($"Item '{name}' not found in dictionary.");
    }

    public static T GetItem<T>(this DatabaseComponentDict<T> dict, string name)
        where T : IEquatable<T>
    {
        foreach (KeyValuePair<Handle, T> kvp in dict)
        {
            if (kvp.Key.Name.Equals(name, StringComparison.Ordinal))
            {
                return kvp.Value;
            }
        }
        throw new KeyNotFoundException($"Item '{name}' not found in dictionary.");
    }
}
