using System.Globalization;

namespace TcfOss.DatabaseManager.Core.Tests;

public class SqlValueListTests
{
    [Fact]
    public void Map_EmptyList_ReturnsNewEmptyListWithoutInvokingSelector()
    {
        SqlValueList<int> source = [];
        int calls = 0;

        SqlValueList<int> mapped = source.Map(value =>
        {
            calls++;
            return value;
        });

        Assert.Empty(mapped);
        Assert.NotSame(source, mapped);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Map_PreservesOrderAndInvokesSelectorOncePerElement()
    {
        SqlValueList<int> source = [3, 1, 2];
        List<int> visited = [];

        SqlValueList<int> mapped = source.Map(value =>
        {
            visited.Add(value);
            return value * 10;
        });

        Assert.Equal(new[] { 3, 1, 2 }, visited);
        Assert.Equal(new[] { 30, 10, 20 }, mapped);
        Assert.Equal(new[] { 3, 1, 2 }, source);
        Assert.NotSame(source, mapped);
    }

    [Fact]
    public void Map_CanChangeElementType()
    {
        SqlValueList<int> source = [10, 200];

        SqlValueList<string> mapped = source.Map(value => value.ToString(CultureInfo.InvariantCulture));

        Assert.Equal(["10", "200"], mapped);
    }

    [Theory]
    [InlineData(false, Label = "Empty list")]
    [InlineData(true, Label = "Populated list")]
    public void Map_NullSelector_Throws(bool populated)
    {
        SqlValueList<int> source = populated ? [1] : [];

        var exception = Assert.Throws<ArgumentNullException>(() => source.Map<string>(null!));

        Assert.Equal("selector", exception.ParamName);
    }
}
