using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Tests.ToolTests;

public class SequenceTests
{
    [Fact]
    public void ToStringWorks()
    {
        var seq = new SqlValueList<Identifier>
        {
            new("col1", QuoteStyle.Ansi),
            new("col2", QuoteStyle.Backticks),
            new("col3"),
            new("col4", QuoteStyle.Brackets),
        };

        var str = seq.ToString();

        Assert.Equal("[\"col1\", `col2`, col3, [col4]]", str);
    }

    [Fact]
    public void ToSql_WithIHaveSql()
    {
        var seq = new SqlValueList<Identifier?>
        {
            new("col1", QuoteStyle.Ansi),
            new("col2", QuoteStyle.Backticks),
            new("col3"),
            new("col4", QuoteStyle.Brackets),
            null
        };

        var str = seq.ToSql();

        Assert.Equal("\"col1\", `col2`, col3, [col4], ", str);
    }

    [Fact]
    public void ToSql_WithoutIHaveSql()
    {
        var seq = new SqlValueList<string?>
        {
            "col1",
            "col2",
            "col3",
            "col4",
            null
        };

        var str = seq.ToSql();

        Assert.Equal("col1, col2, col3, col4, ", str);
    }

    [Fact]
    public void CloneWorks()
    {
        var seq = new SqlValueList<Identifier>
        {
            new("col1", QuoteStyle.Ansi),
            new("col2", QuoteStyle.Backticks),
            new("col3"),
            new("col4", QuoteStyle.Brackets),
        };

        var cloned = seq.Clone();

        Assert.Equal(seq, cloned);
        Assert.NotSame(seq, cloned);

        var cloned2 = (seq as ICloneable).Clone();

        Assert.Equal(seq, cloned2);
        Assert.NotSame(seq, cloned2);
    }

    [Fact]
    public void CloneWorksWithICloneableItems()
    {
        var seq = new SqlValueList<TestRec>
        {
            new("val1"),
            new("val2"),
            new("val3"),
        };

        var cloned = seq.Clone();

        Assert.Equal(seq, cloned);
        Assert.NotSame(seq, cloned);

        for (var i = 0; i < seq.Count; i++)
        {
            Assert.NotSame(seq[i], cloned[i]);
        }
    }

    class TestRec(string myVal) : ICloneable, IEquatable<TestRec>
    {
        private string MyVal { get; } = myVal;

        private TestRec Clone()
        {
            return new TestRec(MyVal);
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public bool Equals(TestRec? other)
        {
            return other != null && MyVal == other.MyVal;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TestRec);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MyVal);
        }
    }
}
