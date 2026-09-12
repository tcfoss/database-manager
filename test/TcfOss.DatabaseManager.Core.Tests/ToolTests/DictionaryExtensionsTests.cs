using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Extensions;

namespace TcfOss.DatabaseManager.Core.Tests.ToolTests;

public class DictionaryExtensionsTests
{
    private readonly DatabaseObjectDict<string> _objectHandleDict;
    private readonly Dictionary<Identifier, string> _idDict;
    private readonly Dictionary<ColumnIdentifier, string> _columnDict;

    public DictionaryExtensionsTests()
    {
        var objectIdDict = new Dictionary<ObjectIdentifier, string>
        {
            [ObjectIdentifier.FromStrings("def", "schema1", "table1")] = "Table 1",
            [ObjectIdentifier.FromStrings("def", "schema1", "table2")] = "Table 2",
            [ObjectIdentifier.FromStrings("def", "schema1", "table3")] = "Table 3",
            [ObjectIdentifier.FromStrings("def", "schema2", "table1", QuoteStyle.Brackets)] = "Table 4",
            [ObjectIdentifier.FromStrings("def", "schema2", "table2", QuoteStyle.Brackets)] = "Table 5",
            [ObjectIdentifier.FromStrings("def2", "schema1", "table1")] = "Table 6"
        };

        _objectHandleDict = [];
        foreach (var kvp in objectIdDict)
        {
            _objectHandleDict[ObjectHandle.Create(kvp.Key, NameHandling.None)] = kvp.Value;
        }

        _idDict = new Dictionary<Identifier, string>
        {
            [new Identifier("id1")] = "Value 1",
            [new Identifier("id2", QuoteStyle.Brackets)] = "Value 2"
        };

        _columnDict = new Dictionary<ColumnIdentifier, string>
        {
            [ColumnIdentifier.FromStrings("def", "schema1", "table1", "col1")] = "Column 1",
            [ColumnIdentifier.FromStrings("def", "schema1", "table1", "col2", QuoteStyle.Brackets)] = "Column 2",
            [ColumnIdentifier.FromStrings("def", "schema1", "table2", "col1", QuoteStyle.Brackets)] = "Column 3"
        };
    }

    [Fact]
    public void Test_Get_Objects()
    {
        var result = _objectHandleDict.GetObjects("table1");
        Assert.Equal(3, result.Count);
        Assert.Contains("Table 1", result);
        Assert.Contains("Table 4", result);
        Assert.Contains("Table 6", result);
    }

    [Fact]
    public void Test_Get_Object_Unique_Name()
    {
        var result = _objectHandleDict.GetObject("table3");
        Assert.Equal("Table 3", result);
    }

    [Fact]
    public void Test_Get_Object_Unique_With_Schema()
    {
        var result = _objectHandleDict.GetObject("table2", "schema2");
        Assert.Equal("Table 5", result);
    }

    [Fact]
    public void Test_Get_Object_Duplicate_Fails()
    {
        var result = Assert.Throws<InvalidOperationException>(() => _objectHandleDict.GetObject("table1"));
        Assert.Equal("Multiple objects named 'table1' found in dictionary.", result.Message);
    }

    [Fact]
    public void Test_Get_Object_Duplicate_With_Schema_Fails()
    {
        var result = Assert.Throws<InvalidOperationException>(() => _objectHandleDict.GetObject("table1", "schema1"));
        Assert.Equal("Multiple objects named 'schema1.table1' found in dictionary.", result.Message);
    }

    [Fact]
    public void Test_Get_Object_Missing_Fails()
    {
        var result = Assert.Throws<KeyNotFoundException>(() => _objectHandleDict.GetObject("nonexistent"));
        Assert.Equal("Object 'nonexistent' not found in dictionary.", result.Message);
    }

    [Fact]
    public void Test_Get_Object_Missing_With_Schema_Fails()
    {
        var result = Assert.Throws<KeyNotFoundException>(() => _objectHandleDict.GetObject("nonexistent", "schema1"));
        Assert.Equal("Object 'schema1.nonexistent' not found in dictionary.", result.Message);
    }

    [Fact]
    public void Test_Get_Column()
    {
        var result = _columnDict.GetColumn("col2");
        Assert.Equal("Column 2", result);
    }

    [Fact]
    public void Test_Get_Column_Missing_Fails()
    {
        var result = Assert.Throws<KeyNotFoundException>(() => _columnDict.GetColumn("nonexistent"));
        Assert.Equal("Column 'nonexistent' not found in dictionary.", result.Message);
    }

    [Fact]
    public void Test_Get_Identifier()
    {
        var result1 = _idDict.GetItem("id1");
        Assert.Equal("Value 1", result1);

        var result2 = _idDict.GetItem("id2");
        Assert.Equal("Value 2", result2);
    }

    [Fact]
    public void Test_Get_Identifier_Missing_Fails()
    {
        var result = Assert.Throws<KeyNotFoundException>(() => _idDict.GetItem("nonexistent"));
        Assert.Equal("Item 'nonexistent' not found in dictionary.", result.Message);
    }
}
