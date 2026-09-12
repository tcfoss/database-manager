using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.Tests.Common;

public class ObjectHandleTests
{
    private static readonly CatalogIdentifier s_catalog = new("def");
    private static readonly SchemaIdentifier s_schema = new("myschema", s_catalog);

    private static ObjectHandle GetObjectHandle(string catalog, string schema, string name)
    {
        return new ObjectHandle(catalog, schema, name);
    }

    // === Create from ObjectName (SqlValueList overload) ===

    [Fact]
    public void Create_From_ObjectName_Three_Parts()
    {
        var name = new ObjectName([
            new Identifier("cat"),
            new Identifier("sch"),
            new Identifier("tbl"),
        ]);

        ObjectHandle result = ObjectHandle.Create(name, s_schema, NameHandling.None);

        Assert.Equal("cat", result.Catalog);
        Assert.Equal("sch", result.Schema);
        Assert.Equal("tbl", result.Name);
    }

    [Fact]
    public void Create_From_ObjectName_Overload()
    {
        var name = new ObjectName([new Identifier("tbl")]);

        ObjectHandle result = ObjectHandle.Create(name, s_schema, NameHandling.None);

        Assert.Equal("def", result.Catalog);
        Assert.Equal("myschema", result.Schema);
        Assert.Equal("tbl", result.Name);
    }

    [Fact]
    public void Create_From_SqlValueList_Empty_Throws()
    {
        SqlValueList<Identifier> parts = [];

        Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => ObjectHandle.Create(parts, s_schema, NameHandling.None));
    }

    // === GetNamePart with various NameHandling values ===

    [Theory]
    [InlineData(NameHandling.Uppercase, QuoteStyle.None, "HELLO")]
    [InlineData(NameHandling.Uppercase, QuoteStyle.Backticks, "HELLO")]
    [InlineData(NameHandling.UppercaseUnlessQuoted, QuoteStyle.None, "HELLO")]
    [InlineData(NameHandling.UppercaseUnlessQuoted, QuoteStyle.Backticks, "Hello")]
    [InlineData(NameHandling.UppercaseUnlessQuoted, QuoteStyle.Ansi, "Hello")]
    public void GetNamePart_Uppercase_Variants(NameHandling handling, QuoteStyle quoteStyle, string expected)
    {
        var identifier = new Identifier("Hello", quoteStyle);

        string result = Handle.GetNamePart(identifier, handling);

        Assert.Equal(expected, result);
    }

    // === Comparison operators ===

    [Fact]
    public void CompareTo_Catalog_Differs()
    {
        ObjectHandle a = GetObjectHandle("aaa", "schema", "name");
        ObjectHandle b = GetObjectHandle("zzz", "schema", "name");

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
    }

    [Fact]
    public void CompareTo_Schema_Differs()
    {
        ObjectHandle a = GetObjectHandle("cat", "aaa", "name");
        ObjectHandle b = GetObjectHandle("cat", "zzz", "name");

        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void CompareTo_Equal()
    {
        ObjectHandle a = GetObjectHandle("cat", "sch", "tbl");
        ObjectHandle b = GetObjectHandle("cat", "sch", "tbl");

        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void LessThan_Operator()
    {
        ObjectHandle a = GetObjectHandle("a", "s", "n");
        ObjectHandle b = GetObjectHandle("b", "s", "n");
        ObjectHandle a2 = GetObjectHandle("a", "s", "n");

        Assert.True(a < b);
        Assert.False(b < a);
        Assert.False(a < a2);
    }

    [Fact]
    public void LessThanOrEqual_Operator()
    {
        ObjectHandle a = GetObjectHandle("a", "s", "n");
        ObjectHandle b = GetObjectHandle("b", "s", "n");
        ObjectHandle a2 = GetObjectHandle("a", "s", "n");

        Assert.True(a <= b);
        Assert.True(a <= a2);
        Assert.False(b <= a);
    }

    [Fact]
    public void GreaterThan_Operator()
    {
        ObjectHandle a = GetObjectHandle("a", "s", "n");
        ObjectHandle b = GetObjectHandle("b", "s", "n");
        ObjectHandle a2 = GetObjectHandle("a", "s", "n");

        Assert.True(b > a);
        Assert.False(a > b);
        Assert.False(a > a2);
    }

    [Fact]
    public void GreaterThanOrEqual_Operator()
    {
        ObjectHandle a = GetObjectHandle("a", "s", "n");
        ObjectHandle b = GetObjectHandle("b", "s", "n");
        ObjectHandle a2 = GetObjectHandle("a", "s", "n");

        Assert.True(b >= a);
        Assert.True(a >= a2);
        Assert.False(a >= b);
    }
}
