using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.DatabaseManager.Core;

public class DatabaseComponentDict<TVal>
    : ICloneable, IEquatable<DatabaseComponentDict<TVal>>, IEnumerable<KeyValuePair<Handle, TVal>>
    where TVal : IEquatable<TVal>
{
    private readonly NameHandling _nameHandling;
    private readonly ValueDict<Handle, TVal> _dict;

    public DatabaseComponentDict(NameHandling nameHandling = NameHandling.None)
    {
        _nameHandling = nameHandling;
        _dict = [];
    }

    public DatabaseComponentDict(DatabaseComponentDict<TVal> previous) : this(previous._nameHandling)
    {
        _dict = new ValueDict<Handle, TVal>(previous._dict);
    }

    public DatabaseComponentDict(ValueDict<Handle, TVal> source, NameHandling nameHandling = NameHandling.None)
    {
        _nameHandling = nameHandling;
        _dict = source;
    }

    public DatabaseComponentDict(IEnumerable<KeyValuePair<Handle, TVal>> source, NameHandling nameHandling = NameHandling.None)
    {
        _nameHandling = nameHandling;
        _dict = new ValueDict<Handle, TVal>(source);
    }

    public TVal this[Handle key]
    {
        get => _dict[key];
        set => _dict[key] = value;
    }

    public TVal this[Identifier identifier]
    {
        get => _dict[Handle.Create(identifier, _nameHandling)];
        set => _dict[Handle.Create(identifier, _nameHandling)] = value;
    }

    public Dictionary<Handle, TVal>.KeyCollection Keys => _dict.Keys;

    public Dictionary<Handle, TVal>.ValueCollection Values => _dict.Values;

    public bool ContainsKey(Handle key)
    {
        return _dict.ContainsKey(key);
    }

    public bool ContainsKey(Identifier identifier)
    {
        return _dict.ContainsKey(Handle.Create(identifier, _nameHandling));
    }

    public TVal? GetValueOrDefault(Handle key)
    {
        return _dict.GetValueOrDefault(key);
    }

    public TVal GetValueOrDefault(Handle key, TVal defaultValue)
    {
        return _dict.GetValueOrDefault(key, defaultValue);
    }

    public TVal? GetValueOrDefault(Identifier identifier)
    {
        return _dict.GetValueOrDefault(Handle.Create(identifier, _nameHandling));
    }

    public TVal GetValueOrDefault(Identifier identifier, TVal defaultValue)
    {
        return _dict.GetValueOrDefault(Handle.Create(identifier, _nameHandling), defaultValue);
    }

    public bool TryGetValue(Handle key, [NotNullWhen(true)] out TVal? value)
    {
        return _dict.TryGetValue(key, out value);
    }

    public bool TryGetValue(Identifier identifier, [NotNullWhen(true)] out TVal? value)
    {
        return _dict.TryGetValue(Handle.Create(identifier, _nameHandling), out value);
    }

    public void Add(Handle key, TVal value)
    {
        _dict.Add(key, value);
    }

    public void Add(Identifier identifier, TVal value)
    {
        _dict.Add(Handle.Create(identifier, _nameHandling), value);
    }

    public bool Remove(Handle key)
    {
        return _dict.Remove(key);
    }

    public bool Remove(Identifier identifier)
    {
        return _dict.Remove(Handle.Create(identifier, _nameHandling));
    }

    public int Count => _dict.Count;

    public IEnumerator<KeyValuePair<Handle, TVal>> GetEnumerator()
    {
        return _dict.GetEnumerator();
    }

    IEnumerator<KeyValuePair<Handle, TVal>> IEnumerable<KeyValuePair<Handle, TVal>>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private bool Equals(DatabaseComponentDict<TVal>? other)
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
        return Equals(obj as DatabaseComponentDict<TVal>);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_dict);
        return hash.ToHashCode();
    }

    public DatabaseComponentDict<TVal> Clone()
    {
        ValueDict<Handle, TVal> dataClone = _dict.Clone();
        return new DatabaseComponentDict<TVal>(dataClone, _nameHandling);
    }

    object ICloneable.Clone()
    {
        return Clone();
    }

    bool IEquatable<DatabaseComponentDict<TVal>>.Equals(DatabaseComponentDict<TVal>? other)
    {
        return Equals(other);
    }
}
