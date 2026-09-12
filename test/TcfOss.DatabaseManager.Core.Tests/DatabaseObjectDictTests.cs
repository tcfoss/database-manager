using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.DatabaseManager.Core.Tests;

public class DatabaseObjectDictTests
{
    private static readonly CatalogIdentifier s_catalog = new("def");
    private static readonly SchemaIdentifier s_schema = new("myschema", s_catalog);

    private static ObjectIdentifier MakeId(string name)
    {
        return new ObjectIdentifier(name, s_schema);
    }

    private static ObjectHandle MakeHandle(string name)
    {
        return ObjectHandle.Create(MakeId(name), NameHandling.None);
    }

    // === ContainsKey(ObjectIdentifier) ===

    [Fact]
    public void ContainsKey_ObjectIdentifier_Returns_True_When_Present()
    {
        var dict = new DatabaseObjectDict<int>
        {
            { MakeId("table1"), 42 }
        };

        Assert.True(dict.ContainsKey(MakeId("table1")));
    }

    [Fact]
    public void ContainsKey_ObjectIdentifier_Returns_False_When_Absent()
    {
        var dict = new DatabaseObjectDict<int>();

        Assert.False(dict.ContainsKey(MakeId("nonexistent")));
    }

    // === GetValueOrDefault(ObjectHandle, TVal) ===

    [Fact]
    public void GetValueOrDefault_Handle_With_Default_Returns_Value_When_Present()
    {
        var dict = new DatabaseObjectDict<int>();
        ObjectHandle handle = MakeHandle("tbl");
        dict.Add(handle, 99);

        int result = dict.GetValueOrDefault(handle, -1);

        Assert.Equal(99, result);
    }

    [Fact]
    public void GetValueOrDefault_Handle_With_Default_Returns_Default_When_Absent()
    {
        var dict = new DatabaseObjectDict<int>();
        ObjectHandle handle = MakeHandle("missing");

        int result = dict.GetValueOrDefault(handle, -1);

        Assert.Equal(-1, result);
    }

    // === GetValueOrDefault(ObjectIdentifier, TVal) ===

    [Fact]
    public void GetValueOrDefault_Identifier_With_Default_Returns_Value()
    {
        var dict = new DatabaseObjectDict<int>
        {
            { MakeId("tbl"), 55 }
        };

        int result = dict.GetValueOrDefault(MakeId("tbl"), -1);

        Assert.Equal(55, result);
    }

    [Fact]
    public void GetValueOrDefault_Identifier_With_Default_Returns_Default()
    {
        var dict = new DatabaseObjectDict<int>();

        int result = dict.GetValueOrDefault(MakeId("missing"), -1);

        Assert.Equal(-1, result);
    }

    // === Remove(ObjectHandle) ===

    [Fact]
    public void Remove_Handle_Returns_True_And_Removes()
    {
        var dict = new DatabaseObjectDict<int>();
        ObjectHandle handle = MakeHandle("tbl");
        dict.Add(handle, 1);

        bool removed = dict.Remove(handle);

        Assert.True(removed);
        Assert.False(dict.ContainsKey(handle));
    }

    [Fact]
    public void Remove_Handle_Returns_False_When_Absent()
    {
        var dict = new DatabaseObjectDict<int>();

        bool removed = dict.Remove(MakeHandle("missing"));

        Assert.False(removed);
    }

    // === Remove(ObjectIdentifier) ===

    [Fact]
    public void Remove_ObjectIdentifier_Returns_True_And_Removes()
    {
        var dict = new DatabaseObjectDict<int>
        {
            { MakeId("tbl"), 1 }
        };

        bool removed = dict.Remove(MakeId("tbl"));

        Assert.True(removed);
        Assert.False(dict.ContainsKey(MakeId("tbl")));
    }

    [Fact]
    public void Remove_ObjectIdentifier_Returns_False_When_Absent()
    {
        var dict = new DatabaseObjectDict<int>();

        bool removed = dict.Remove(MakeId("missing"));

        Assert.False(removed);
    }

    // === Equality ===

    [Fact]
    public void Equals_Same_Contents_Returns_True()
    {
        var dict1 = new DatabaseObjectDict<int>();
        var dict2 = new DatabaseObjectDict<int>();
        dict1.Add(MakeHandle("a"), 1);
        dict2.Add(MakeHandle("a"), 1);

        Assert.True(((IEquatable<DatabaseObjectDict<int>>)dict1).Equals(dict2));
    }

    [Fact]
    public void Equals_Different_Contents_Returns_False()
    {
        var dict1 = new DatabaseObjectDict<int>();
        var dict2 = new DatabaseObjectDict<int>();
        dict1.Add(MakeHandle("a"), 1);
        dict2.Add(MakeHandle("a"), 2);

        Assert.False(((IEquatable<DatabaseObjectDict<int>>)dict1).Equals(dict2));
    }

    [Fact]
    public void Equals_Null_Returns_False()
    {
        var dict = new DatabaseObjectDict<int>();

        Assert.False(dict.Equals(null));
    }

    [Fact]
    public void Equals_Different_NameHandling_Returns_False()
    {
        var dict1 = new DatabaseObjectDict<int>();
        var dict2 = new DatabaseObjectDict<int>(NameHandling.Lowercase);

        Assert.False(dict1.Equals(dict2));
    }

    // === GetHashCode ===

    [Fact]
    public void GetHashCode_Same_Contents_Same_Hash()
    {
        var dict1 = new DatabaseObjectDict<int>();
        var dict2 = new DatabaseObjectDict<int>();
        dict1.Add(MakeHandle("a"), 1);
        dict2.Add(MakeHandle("a"), 1);

        Assert.Equal(dict1.GetHashCode(), dict2.GetHashCode());
    }

    // === Clone (ICloneable) ===

    [Fact]
    public void Clone_ICloneable_Returns_Equal_Copy()
    {
        var dict = new DatabaseObjectDict<int>
        {
            { MakeHandle("x"), 42 }
        };

        var clone = (DatabaseObjectDict<int>)((ICloneable)dict).Clone();

        Assert.True(dict.Equals(clone));
        Assert.Equal(42, clone[MakeHandle("x")]);
    }

    // === Clone (generic) ===

    [Fact]
    public void Clone_Generic_Returns_Equal_Independent_Copy()
    {
        var original = new DatabaseObjectDict<int>
        {
            { MakeHandle("x"), 42 }
        };

        DatabaseObjectDict<int> clone = original.Clone();

        Assert.True(original.Equals(clone));
        Assert.Equal(42, clone[MakeHandle("x")]);
    }

    // === Constructors ===

    [Fact]
    public void Constructor_Copy_Preserves_Contents()
    {
        var original = new DatabaseObjectDict<int>
        {
            { MakeHandle("a"), 10 },
            { MakeHandle("b"), 20 }
        };

        var copy = new DatabaseObjectDict<int>(original);

        Assert.Equal(2, copy.Count);
        Assert.Equal(10, copy[MakeHandle("a")]);
        Assert.Equal(20, copy[MakeHandle("b")]);
    }

    [Fact]
    public void Constructor_IEnumerable_Source_Preserves_Contents()
    {
        List<KeyValuePair<ObjectHandle, int>> source =
        [
            new(MakeHandle("a"), 10),
            new(MakeHandle("b"), 20),
        ];

        var dict = new DatabaseObjectDict<int>(source);

        Assert.Equal(2, dict.Count);
        Assert.Equal(10, dict[MakeHandle("a")]);
        Assert.Equal(20, dict[MakeHandle("b")]);
    }

    [Fact]
    public void Constructor_ValueDict_Source_Preserves_Contents()
    {
        var source = new ValueDict<ObjectHandle, int>
        {
            { MakeHandle("a"), 10 },
            { MakeHandle("b"), 20 }
        };

        var dict = new DatabaseObjectDict<int>(source);

        Assert.Equal(2, dict.Count);
        Assert.Equal(10, dict[MakeHandle("a")]);
        Assert.Equal(20, dict[MakeHandle("b")]);
    }
}
