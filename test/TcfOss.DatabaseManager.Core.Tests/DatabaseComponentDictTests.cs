using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.DatabaseManager.Core.Tests;

public class DatabaseComponentDictTests
{
    private static Identifier MakeId(string name)
    {
        return new Identifier(name);
    }

    private static Handle MakeHandle(string name)
    {
        return Handle.Create(MakeId(name), NameHandling.None);
    }

    // === ContainsKey(Identifier) ===

    [Fact]
    public void ContainsKey_Identifier_Returns_True_When_Present()
    {
        var dict = new DatabaseComponentDict<int>
        {
            { MakeId("col1"), 42 }
        };

        Assert.True(dict.ContainsKey(MakeId("col1")));
    }

    [Fact]
    public void ContainsKey_Identifier_Returns_False_When_Absent()
    {
        var dict = new DatabaseComponentDict<int>();

        Assert.False(dict.ContainsKey(MakeId("nonexistent")));
    }

    // === GetValueOrDefault(Handle, TVal) ===

    [Fact]
    public void GetValueOrDefault_Handle_With_Default_Returns_Value_When_Present()
    {
        var dict = new DatabaseComponentDict<int>();
        Handle handle = MakeHandle("col");
        dict.Add(handle, 99);

        int result = dict.GetValueOrDefault(handle, -1);

        Assert.Equal(99, result);
    }

    [Fact]
    public void GetValueOrDefault_Handle_With_Default_Returns_Default_When_Absent()
    {
        var dict = new DatabaseComponentDict<int>();
        Handle handle = MakeHandle("missing");

        int result = dict.GetValueOrDefault(handle, -1);

        Assert.Equal(-1, result);
    }

    // === GetValueOrDefault(Identifier, TVal) ===

    [Fact]
    public void GetValueOrDefault_Identifier_With_Default_Returns_Value()
    {
        var dict = new DatabaseComponentDict<int>
        {
            { MakeId("col"), 55 }
        };

        int result = dict.GetValueOrDefault(MakeId("col"), -1);

        Assert.Equal(55, result);
    }

    [Fact]
    public void GetValueOrDefault_Identifier_With_Default_Returns_Default()
    {
        var dict = new DatabaseComponentDict<int>();

        int result = dict.GetValueOrDefault(MakeId("missing"), -1);

        Assert.Equal(-1, result);
    }

    // === Remove(Handle) ===

    [Fact]
    public void Remove_Handle_Returns_True_And_Removes()
    {
        var dict = new DatabaseComponentDict<int>();
        Handle handle = MakeHandle("col");
        dict.Add(handle, 1);

        bool removed = dict.Remove(handle);

        Assert.True(removed);
        Assert.False(dict.ContainsKey(handle));
    }

    [Fact]
    public void Remove_Handle_Returns_False_When_Absent()
    {
        var dict = new DatabaseComponentDict<int>();

        bool removed = dict.Remove(MakeHandle("missing"));

        Assert.False(removed);
    }

    // === Remove(Identifier) ===

    [Fact]
    public void Remove_Identifier_Returns_True_And_Removes()
    {
        var dict = new DatabaseComponentDict<int>
        {
            { MakeId("col"), 1 }
        };

        bool removed = dict.Remove(MakeId("col"));

        Assert.True(removed);
        Assert.False(dict.ContainsKey(MakeId("col")));
    }

    [Fact]
    public void Remove_Identifier_Returns_False_When_Absent()
    {
        var dict = new DatabaseComponentDict<int>();

        bool removed = dict.Remove(MakeId("missing"));

        Assert.False(removed);
    }

    // === Equality ===

    [Fact]
    public void Equals_Same_Contents_Returns_True()
    {
        var dict1 = new DatabaseComponentDict<int>();
        var dict2 = new DatabaseComponentDict<int>();
        dict1.Add(MakeHandle("a"), 1);
        dict2.Add(MakeHandle("a"), 1);

        Assert.True(((IEquatable<DatabaseComponentDict<int>>)dict1).Equals(dict2));
    }

    [Fact]
    public void Equals_Different_Contents_Returns_False()
    {
        var dict1 = new DatabaseComponentDict<int>();
        var dict2 = new DatabaseComponentDict<int>();
        dict1.Add(MakeHandle("a"), 1);
        dict2.Add(MakeHandle("a"), 2);

        Assert.False(((IEquatable<DatabaseComponentDict<int>>)dict1).Equals(dict2));
    }

    [Fact]
    public void Equals_Null_Returns_False()
    {
        var dict = new DatabaseComponentDict<int>();

        Assert.False(dict.Equals(null));
    }

    [Fact]
    public void Equals_Different_NameHandling_Returns_False()
    {
        var dict1 = new DatabaseComponentDict<int>();
        var dict2 = new DatabaseComponentDict<int>(NameHandling.Lowercase);

        Assert.False(dict1.Equals(dict2));
    }

    // === GetHashCode ===

    [Fact]
    public void GetHashCode_Same_Contents_Same_Hash()
    {
        var dict1 = new DatabaseComponentDict<int>();
        var dict2 = new DatabaseComponentDict<int>();
        dict1.Add(MakeHandle("a"), 1);
        dict2.Add(MakeHandle("a"), 1);

        Assert.Equal(dict1.GetHashCode(), dict2.GetHashCode());
    }

    // === Clone (ICloneable) ===

    [Fact]
    public void Clone_ICloneable_Returns_Equal_Copy()
    {
        var dict = new DatabaseComponentDict<int>
        {
            { MakeHandle("x"), 42 }
        };

        var clone = (DatabaseComponentDict<int>)((ICloneable)dict).Clone();

        Assert.True(dict.Equals(clone));
        Assert.Equal(42, clone[MakeHandle("x")]);
    }

    // === Clone (generic) ===

    [Fact]
    public void Clone_Generic_Returns_Equal_Independent_Copy()
    {
        var original = new DatabaseComponentDict<int>
        {
            { MakeHandle("x"), 42 }
        };

        DatabaseComponentDict<int> clone = original.Clone();

        Assert.True(original.Equals(clone));
        Assert.Equal(42, clone[MakeHandle("x")]);
    }

    // === Constructors ===

    [Fact]
    public void Constructor_Copy_Preserves_Contents()
    {
        var original = new DatabaseComponentDict<int>
        {
            { MakeHandle("a"), 10 },
            { MakeHandle("b"), 20 }
        };

        var copy = new DatabaseComponentDict<int>(original);

        Assert.Equal(2, copy.Count);
        Assert.Equal(10, copy[MakeHandle("a")]);
        Assert.Equal(20, copy[MakeHandle("b")]);
    }

    [Fact]
    public void Constructor_IEnumerable_Source_Preserves_Contents()
    {
        List<KeyValuePair<Handle, int>> source =
        [
            new(MakeHandle("a"), 10),
            new(MakeHandle("b"), 20),
        ];

        var dict = new DatabaseComponentDict<int>(source);

        Assert.Equal(2, dict.Count);
        Assert.Equal(10, dict[MakeHandle("a")]);
        Assert.Equal(20, dict[MakeHandle("b")]);
    }

    [Fact]
    public void Constructor_ValueDict_Source_Preserves_Contents()
    {
        var source = new ValueDict<Handle, int>
        {
            { MakeHandle("a"), 10 },
            { MakeHandle("b"), 20 }
        };

        var dict = new DatabaseComponentDict<int>(source);

        Assert.Equal(2, dict.Count);
        Assert.Equal(10, dict[MakeHandle("a")]);
        Assert.Equal(20, dict[MakeHandle("b")]);
    }

    // === Indexer (Identifier) ===

    [Fact]
    public void Indexer_Identifier_Get_Returns_Value()
    {
        var dict = new DatabaseComponentDict<int>
        {
            { MakeId("col"), 77 }
        };

        Assert.Equal(77, dict[MakeId("col")]);
    }

    [Fact]
    public void Indexer_Identifier_Set_Assigns_Value()
    {
        var dict = new DatabaseComponentDict<int>
        {
            { MakeId("col"), 1 }
        };

        dict[MakeId("col")] = 99;

        Assert.Equal(99, dict[MakeId("col")]);
    }

    // === GetValueOrDefault(Identifier) ===

    [Fact]
    public void GetValueOrDefault_Identifier_Returns_Value_When_Present()
    {
        var dict = new DatabaseComponentDict<int>
        {
            { MakeId("col"), 55 }
        };

        int? result = dict.GetValueOrDefault(MakeId("col"));

        Assert.Equal(55, result);
    }

    [Fact]
    public void GetValueOrDefault_Identifier_Returns_Default_When_Absent()
    {
        var dict = new DatabaseComponentDict<int>();

        int? result = dict.GetValueOrDefault(MakeId("missing"));

        Assert.Equal(0, result);
    }

    // === TryGetValue ===

    [Fact]
    public void TryGetValue_Handle_Returns_True_And_Value_When_Present()
    {
        var dict = new DatabaseComponentDict<int>();
        Handle handle = MakeHandle("col");
        dict.Add(handle, 42);

        bool found = dict.TryGetValue(handle, out int value);

        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_Handle_Returns_False_When_Absent()
    {
        var dict = new DatabaseComponentDict<int>();

        bool found = dict.TryGetValue(MakeHandle("missing"), out int value);

        Assert.False(found);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetValue_Identifier_Returns_True_And_Value_When_Present()
    {
        var dict = new DatabaseComponentDict<int>
        {
            { MakeId("col"), 42 }
        };

        bool found = dict.TryGetValue(MakeId("col"), out int value);

        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_Identifier_Returns_False_When_Absent()
    {
        var dict = new DatabaseComponentDict<int>();

        bool found = dict.TryGetValue(MakeId("missing"), out int value);

        Assert.False(found);
        Assert.Equal(0, value);
    }
}
