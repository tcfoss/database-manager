using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.DatabaseManager.Core;

public class DatabaseObjectDict<TVal>
    : ICloneable, IEquatable<DatabaseObjectDict<TVal>>, IEnumerable<KeyValuePair<ObjectHandle, TVal>>
    where TVal : IEquatable<TVal>
{
    private readonly NameHandling _nameHandling;
    private readonly ValueDict<ObjectHandle, TVal> _dict;

    public DatabaseObjectDict(NameHandling nameHandling = NameHandling.None)
    {
        _nameHandling = nameHandling;
        _dict = [];
    }

    public DatabaseObjectDict(DatabaseObjectDict<TVal> previous) : this(previous._nameHandling)
    {
        _dict = new ValueDict<ObjectHandle, TVal>(previous._dict);
    }

    public DatabaseObjectDict(ValueDict<ObjectHandle, TVal> source, NameHandling nameHandling = NameHandling.None)
    {
        _nameHandling = nameHandling;
        _dict = source;
    }

    public DatabaseObjectDict(IEnumerable<KeyValuePair<ObjectHandle, TVal>> source, NameHandling nameHandling = NameHandling.None)
    {
        _nameHandling = nameHandling;
        _dict = new ValueDict<ObjectHandle, TVal>(source);
    }

    public TVal this[ObjectHandle key]
    {
        get => _dict[key];
        set => _dict[key] = value;
    }

    public TVal this[ObjectIdentifier identifier]
    {
        get => _dict[ObjectHandle.Create(identifier, _nameHandling)];
        set => _dict[ObjectHandle.Create(identifier, _nameHandling)] = value;
    }

    public Dictionary<ObjectHandle, TVal>.KeyCollection Keys => _dict.Keys;

    public Dictionary<ObjectHandle, TVal>.ValueCollection Values => _dict.Values;

    public bool ContainsKey(ObjectHandle key)
    {
        return _dict.ContainsKey(key);
    }

    public bool ContainsKey(ObjectIdentifier identifier)
    {
        return _dict.ContainsKey(ObjectHandle.Create(identifier, _nameHandling));
    }

    public TVal? GetValueOrDefault(ObjectHandle key)
    {
        return _dict.GetValueOrDefault(key);
    }

    public TVal GetValueOrDefault(ObjectHandle key, TVal defaultValue)
    {
        return _dict.GetValueOrDefault(key, defaultValue);
    }

    public TVal? GetValueOrDefault(ObjectIdentifier identifier)
    {
        return _dict.GetValueOrDefault(ObjectHandle.Create(identifier, _nameHandling));
    }

    public TVal GetValueOrDefault(ObjectIdentifier identifier, TVal defaultValue)
    {
        return _dict.GetValueOrDefault(ObjectHandle.Create(identifier, _nameHandling), defaultValue);
    }

    public bool TryGetValue(ObjectHandle key, [NotNullWhen(true)] out TVal? value)
    {
        return _dict.TryGetValue(key, out value);
    }

    public bool TryGetValue(ObjectIdentifier identifier, [NotNullWhen(true)] out TVal? value)
    {
        return _dict.TryGetValue(ObjectHandle.Create(identifier, _nameHandling), out value);
    }

    public void Add(ObjectHandle key, TVal value)
    {
        _dict.Add(key, value);
    }

    public void Add(ObjectIdentifier identifier, TVal value)
    {
        _dict.Add(ObjectHandle.Create(identifier, _nameHandling), value);
    }

    public bool Remove(ObjectHandle key)
    {
        return _dict.Remove(key);
    }

    public bool Remove(ObjectIdentifier identifier)
    {
        return _dict.Remove(ObjectHandle.Create(identifier, _nameHandling));
    }

    public int Count => _dict.Count;

    public IEnumerator<KeyValuePair<ObjectHandle, TVal>> GetEnumerator()
    {
        return _dict.GetEnumerator();
    }

    IEnumerator<KeyValuePair<ObjectHandle, TVal>> IEnumerable<KeyValuePair<ObjectHandle, TVal>>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private bool Equals(DatabaseObjectDict<TVal>? other)
    {
        if (other == null)
        {
            return false;
        }

        if (_nameHandling != other._nameHandling)
        {
            return false;
        }

        return _dict.Equals(other._dict);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as DatabaseObjectDict<TVal>);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_dict);
        return hash.ToHashCode();
    }

    public DatabaseObjectDict<TVal> Clone()
    {
        ValueDict<ObjectHandle, TVal> dataClone = _dict.Clone();
        return new DatabaseObjectDict<TVal>(dataClone, _nameHandling);
    }

    object ICloneable.Clone()
    {
        return Clone();
    }

    bool IEquatable<DatabaseObjectDict<TVal>>.Equals(DatabaseObjectDict<TVal>? other)
    {
        return Equals(other);
    }
}
