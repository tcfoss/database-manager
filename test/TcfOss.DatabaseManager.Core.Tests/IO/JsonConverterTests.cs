using Newtonsoft.Json;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Tests.IO;

public class JsonConverterTests
{
    private static JsonSerializerSettings GetSettings()
    {
        return new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            Formatting = Newtonsoft.Json.Formatting.Indented,
            Converters =
            {
                new Core.IO.JsonConverters.SchemaIdentifierConverter(),
                new Core.IO.JsonConverters.ObjectIdentifierConverter(),
                new Core.IO.JsonConverters.ColumnIdentifierConverter(),
                new Core.IO.JsonConverters.IdentifierConverter(),
            }
        };
    }

    [Fact]
    public void IdentifierConverterTest()
    {
        var identifier = new Identifier("myIdentifier", QuoteStyle.Backticks);
        var json = JsonConvert.SerializeObject(identifier, GetSettings());
        var deserialized = JsonConvert.DeserializeObject<Identifier>(json, GetSettings());

        Assert.Equal(identifier, deserialized);
    }

    [Fact]
    public void IdentifierConverter_SerializeNull()
    {
        Identifier? identifier = null;
        var json = JsonConvert.SerializeObject(identifier, GetSettings());
        var deserialized = JsonConvert.DeserializeObject<Identifier>(json, GetSettings());

        Assert.Null(deserialized);
    }

    [Fact]
    public void SchemaIdentifierConverterTest()
    {
        var schemaIdentifier = new SchemaIdentifier("mySchema", new CatalogIdentifier("myCatalog", QuoteStyle.Ansi), QuoteStyle.Brackets);
        var json = JsonConvert.SerializeObject(schemaIdentifier, GetSettings());
        var deserialized = JsonConvert.DeserializeObject<SchemaIdentifier>(json, GetSettings());
        Assert.Equal(schemaIdentifier, deserialized);
    }

    [Fact]
    public void SchemaIdentifierConverter_SerializeNull()
    {
        SchemaIdentifier? schemaIdentifier = null;
        var json = JsonConvert.SerializeObject(schemaIdentifier, GetSettings());
        var deserialized = JsonConvert.DeserializeObject<SchemaIdentifier>(json, GetSettings());

        Assert.Null(deserialized);
    }

    [Fact]
    public void ObjectIdentifierConverterTest()
    {
        var objectIdentifier = new ObjectIdentifier("myObject", new SchemaIdentifier("mySchema", new CatalogIdentifier("myCatalog", QuoteStyle.Ansi), QuoteStyle.Brackets));
        var json = JsonConvert.SerializeObject(objectIdentifier, GetSettings());
        var deserialized = JsonConvert.DeserializeObject<ObjectIdentifier>(json, GetSettings());
        Assert.Equal(objectIdentifier, deserialized);
    }

    [Fact]
    public void ObjectIdentifierConverter_SerializeNull()
    {
        ObjectIdentifier? objectIdentifier = null;
        var json = JsonConvert.SerializeObject(objectIdentifier, GetSettings());
        var deserialized = JsonConvert.DeserializeObject<ObjectIdentifier>(json, GetSettings());

        Assert.Null(deserialized);
    }

    [Fact]
    public void ColumnIdentifierConverterTest()
    {
        var columnIdentifier = new ColumnIdentifier("myColumn", new ObjectIdentifier("myObject", new SchemaIdentifier("mySchema", new CatalogIdentifier("myCatalog", QuoteStyle.Ansi), QuoteStyle.Brackets)), QuoteStyle.Backticks);
        var json = JsonConvert.SerializeObject(columnIdentifier, GetSettings());
        var deserialized = JsonConvert.DeserializeObject<ColumnIdentifier>(json, GetSettings());
        Assert.Equal(columnIdentifier, deserialized);
    }

    [Fact]
    public void ColumnIdentifierConverter_SerializeNull()
    {
        ColumnIdentifier? columnIdentifier = null;
        var json = JsonConvert.SerializeObject(columnIdentifier, GetSettings());
        var deserialized = JsonConvert.DeserializeObject<ColumnIdentifier>(json, GetSettings());

        Assert.Null(deserialized);
    }
}
