using System.Globalization;
using Newtonsoft.Json;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO.TypeConverters;

namespace TcfOss.DatabaseManager.Core.Tests.IO;

public class TypeConverterTests
{
    [Fact]
    public void ObjectIdentifierTypeConverter_CanConvertFromString()
    {
        var conv = new ObjectIdentifierTypeConverter();
        Assert.True(conv.CanConvertFrom(null, typeof(string)));
        Assert.False(conv.CanConvertFrom(null, typeof(int)));
    }

    [Fact]
    public void ObjectIdentifierTypeConverter_ConvertFromPlainString()
    {
        var conv = new ObjectIdentifierTypeConverter();
        var input = "mycatalog.schema1.table1";
        var result = conv.ConvertFrom(null, CultureInfo.InvariantCulture, input);
        var expected = new ObjectIdentifier("table1", new SchemaIdentifier("schema1", new CatalogIdentifier("mycatalog")));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ObjectIdentifierTypeConverter_ConvertFromQuotedString()
    {
        var conv = new ObjectIdentifierTypeConverter();
        var input = "`mycatalog`.[schema1].\"table1\"";
        var result = conv.ConvertFrom(null, CultureInfo.InvariantCulture, input);
        var expected = new ObjectIdentifier("table1", new SchemaIdentifier("schema1", new CatalogIdentifier("mycatalog", QuoteStyle.Backticks), QuoteStyle.Brackets), QuoteStyle.Ansi);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ObjectIdentifierTypeConverter_ConvertFromNonString_Throws()
    {
        var conv = new ObjectIdentifierTypeConverter();
        Assert.Throws<NotSupportedException>(() => conv.ConvertFrom(null, CultureInfo.InvariantCulture, 123));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    public void ObjectIdentifierTypeConverter_ConvertFromInvalidString_Throws(string input)
    {
        var conv = new ObjectIdentifierTypeConverter();
        Assert.Throws<JsonException>(() => conv.ConvertFrom(null, CultureInfo.InvariantCulture, input));
    }

    [Fact]
    public void IdentifierTypeConverter_CanConvertFromString()
    {
        var conv = new IdentifierTypeConverter();
        Assert.True(conv.CanConvertFrom(null, typeof(string)));
        Assert.False(conv.CanConvertFrom(null, typeof(int)));
    }

    [Fact]
    public void IdentifierTypeConverter_ConvertFromPlainString()
    {
        var conv = new IdentifierTypeConverter();
        var input = "simple_identifier";
        var result = conv.ConvertFrom(null, CultureInfo.InvariantCulture, input);
        var expected = new Identifier("simple_identifier");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IdentifierTypeConverter_ConvertFromQuotedString()
    {
        var conv = new IdentifierTypeConverter();
        var input = "`quoted_identifier`";
        var result = conv.ConvertFrom(null, CultureInfo.InvariantCulture, input);
        var expected = new Identifier("quoted_identifier", QuoteStyle.Backticks);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IdentifierTypeConverter_ConvertFromNonString_Throws()
    {
        var conv = new IdentifierTypeConverter();
        Assert.Throws<NotSupportedException>(() => conv.ConvertFrom(null, CultureInfo.InvariantCulture, 123));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("a.b.c")]
    public void IdentifierTypeConverter_ConvertFromInvalidString_Throws(string input)
    {
        var conv = new IdentifierTypeConverter();
        Assert.Throws<JsonException>(() => conv.ConvertFrom(null, CultureInfo.InvariantCulture, input));
    }

    [Fact]
    public void ColumnIdentifierTypeConverter_CanConvertFromString()
    {
        var conv = new ColumnIdentifierTypeConverter();
        Assert.True(conv.CanConvertFrom(null, typeof(string)));
        Assert.False(conv.CanConvertFrom(null, typeof(int)));
    }

    [Fact]
    public void ColumnIdentifierTypeConverter_ConvertFromPlainSingle()
    {
        var conv = new ColumnIdentifierTypeConverter();
        var input = "mycatalog.schema1.table1.col1";
        var result = conv.ConvertFrom(null, CultureInfo.InvariantCulture, input);
        var expected = ColumnIdentifier.FromStrings("mycatalog", "schema1", "table1", "col1");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ColumnIdentifierTypeConverter_ConvertFromQuotedSingle()
    {
        var conv = new ColumnIdentifierTypeConverter();
        var input = "`mycatalog`.[schema1].\"table1\".col1";
        var result = conv.ConvertFrom(null, CultureInfo.InvariantCulture, input);
        var expected = new ColumnIdentifier(
            "col1",
            new ObjectIdentifier("table1",
                new SchemaIdentifier("schema1",
                    new CatalogIdentifier("mycatalog", QuoteStyle.Backticks),
                    QuoteStyle.Brackets),
                QuoteStyle.Ansi)
        );
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ColumnIdentifierTypeConverter_ConvertFromNonString_Throws()
    {
        var conv = new ColumnIdentifierTypeConverter();
        Assert.Throws<NotSupportedException>(() => conv.ConvertFrom(null, CultureInfo.InvariantCulture, 123));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a.b")]
    [InlineData("a.b.c")]
    [InlineData("a.b.c.d.e")]
    public void ColumnIdentifierTypeConverter_ConvertFromInvalidString_Throws(string input)
    {
        var conv = new ColumnIdentifierTypeConverter();
        Assert.Throws<JsonException>(() => conv.ConvertFrom(null, CultureInfo.InvariantCulture, input));
    }
}
