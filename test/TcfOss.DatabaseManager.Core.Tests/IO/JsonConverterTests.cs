using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.IO.JsonConverters;

namespace TcfOss.DatabaseManager.Core.Tests.IO;

public class JsonConverterTests
{
    [Theory]
    [InlineData(typeof(Identifier))]
    [InlineData(typeof(SchemaIdentifier))]
    [InlineData(typeof(ObjectIdentifier))]
    [InlineData(typeof(ColumnIdentifier))]
    public void IdentifierConverter_WriteNull_EmitsJsonNull(Type type)
    {
        var settings = GetSettings();
        JsonConverter converter = settings.Converters.First(converter => converter.CanConvert(type));
        var output = new StringBuilder();
        using var textWriter = new StringWriter(output);
        using var writer = new JsonTextWriter(textWriter);

        converter.WriteJson(writer, null, JsonSerializer.Create(settings));

        Assert.Equal("null", output.ToString());
    }

    [Theory]
    [InlineData(typeof(Identifier), "")]
    [InlineData(typeof(Identifier), "name.extra")]
    [InlineData(typeof(SchemaIdentifier), "catalog")]
    [InlineData(typeof(SchemaIdentifier), "catalog.schema.extra")]
    [InlineData(typeof(ObjectIdentifier), "catalog.schema")]
    [InlineData(typeof(ObjectIdentifier), "catalog.schema.object.extra")]
    [InlineData(typeof(ColumnIdentifier), "catalog.schema.object")]
    [InlineData(typeof(ColumnIdentifier), "catalog.schema.object.column.extra")]
    public void IdentifierConverter_InvalidQualifiedName_Throws(Type type, string value)
    {
        string json = JsonConvert.SerializeObject(value);

        Assert.Throws<JsonException>(() => JsonConvert.DeserializeObject(json, type, GetSettings()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DictionaryConverter_AcceptsOnlyItsDictionaryType(bool objectKeys)
    {
        JsonConverter converter = objectKeys ? new DatabaseObjectDictConverter() : new DatabaseComponentDictConverter();
        Type supportedType = objectKeys ? typeof(DatabaseObjectDict<string>) : typeof(DatabaseComponentDict<string>);
        Type unsupportedType = objectKeys ? typeof(DatabaseComponentDict<string>) : typeof(DatabaseObjectDict<string>);

        Assert.True(converter.CanConvert(supportedType));
        Assert.False(converter.CanConvert(unsupportedType));
        Assert.False(converter.CanConvert(typeof(string)));
        Assert.False(converter.CanConvert(typeof(Dictionary<string, string>)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DictionaryConverter_ReadAndWriteNull(bool objectKeys)
    {
        JsonConverter converter = objectKeys ? new DatabaseObjectDictConverter() : new DatabaseComponentDictConverter();
        Type supportedType = objectKeys ? typeof(DatabaseObjectDict<string>) : typeof(DatabaseComponentDict<string>);
        var serializer = JsonSerializer.Create(GetSettings());
        using var reader = new JsonTextReader(new StringReader("null"));
        Assert.True(reader.Read());
        Assert.Null(converter.ReadJson(reader, supportedType, null, serializer));

        var output = new StringBuilder();
        using var textWriter = new StringWriter(output);
        using var writer = new JsonTextWriter(textWriter);
        converter.WriteJson(writer, null, serializer);
        Assert.Equal("null", output.ToString());
    }

    [Theory]
    [InlineData(false, "{}", false)]
    [InlineData(true, "{}", false)]
    [InlineData(false, "{\"Items\":null}", false)]
    [InlineData(true, "{\"Items\":null}", false)]
    [InlineData(false, "{\"NameHandling\":\"Lowercase\"}", true)]
    [InlineData(true, "{\"NameHandling\":\"Lowercase\"}", true)]
    public void DictionaryConverter_MissingItems_DefaultsToEmptyAndPreservesNameHandling(bool objectKeys, string json, bool lowercase)
    {
        var settings = GetSettings();
        if (objectKeys)
        {
            var actual = JsonConvert.DeserializeObject<DatabaseObjectDict<string>>(json, settings);
            Assert.NotNull(actual);
            Assert.Empty(actual);
            actual.Add(ObjectIdentifier.FromStrings("def", "schema", "MixedCase"), "value");
            Assert.Equal(lowercase, actual.ContainsKey(ObjectIdentifier.FromStrings("def", "schema", "mixedcase")));
        }
        else
        {
            var actual = JsonConvert.DeserializeObject<DatabaseComponentDict<string>>(json, settings);
            Assert.NotNull(actual);
            Assert.Empty(actual);
            actual.Add(new Identifier("MixedCase"), "value");
            Assert.Equal(lowercase, actual.ContainsKey(new Identifier("mixedcase")));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DictionaryConverter_RoundTrip_PreservesEntriesAndCaseHandling(bool objectKeys)
    {
        var settings = GetSettings();
        string json;
        if (objectKeys)
        {
            var dictionary = new DatabaseObjectDict<string>(NameHandling.Lowercase)
            {
                { ObjectIdentifier.FromStrings("def", "first_schema", "MixedCase"), "first" },
                { ObjectIdentifier.FromStrings("def", "second_schema", "MixedCase"), "second" }
            };
            json = JsonConvert.SerializeObject(dictionary, settings);
            var actual = JsonConvert.DeserializeObject<DatabaseObjectDict<string>>(json, settings);
            Assert.NotNull(actual);
            Assert.Equal(dictionary, actual);
            Assert.Equal("first", actual[ObjectIdentifier.FromStrings("def", "first_schema", "MIXEDCASE")]);
            Assert.Equal("second", actual[ObjectIdentifier.FromStrings("def", "second_schema", "mixedcase")]);
        }
        else
        {
            var dictionary = new DatabaseComponentDict<string>(NameHandling.Lowercase)
            {
                { new Identifier("First"), "first" }, { new Identifier("Second"), "second" }
            };
            json = JsonConvert.SerializeObject(dictionary, settings);
            var actual = JsonConvert.DeserializeObject<DatabaseComponentDict<string>>(json, settings);
            Assert.NotNull(actual);
            Assert.Equal(dictionary, actual);
            Assert.Equal("first", actual[new Identifier("FIRST")]);
            Assert.Equal("second", actual[new Identifier("second")]);
        }
        var document = JObject.Parse(json);
        Assert.Equal("Lowercase", document.Value<string>("NameHandling"));
        Assert.Equal(2, Assert.IsType<JArray>(document["Items"]).Count);
    }

    [Theory]
    [InlineData(false, "CASCADE")]
    [InlineData(true, "SET NULL")]
    public void StringEnumConverter_RoundTrip_UsesSqlText(bool setNull, string expected)
    {
        ReferentialAction value = setNull ? ReferentialAction.SetNull : ReferentialAction.Cascade;
        var settings = GetSettings();

        string json = JsonConvert.SerializeObject(value, settings);
        var actual = JsonConvert.DeserializeObject<ReferentialAction>(json, settings);

        Assert.Equal(JsonConvert.SerializeObject(expected), json);
        Assert.Equal(value, actual);
    }

    [Fact]
    public void StringEnumConverter_ReadAndWriteNull()
    {
        var converter = new StringEnumConverter<ReferentialAction>();
        var serializer = JsonSerializer.Create(GetSettings());
        using var reader = new JsonTextReader(new StringReader("null"));
        Assert.True(reader.Read());
        Assert.Null(converter.ReadJson(reader, typeof(ReferentialAction), null, false, serializer));

        var output = new StringBuilder();
        using var textWriter = new StringWriter(output);
        using var writer = new JsonTextWriter(textWriter);
        converter.WriteJson(writer, null, serializer);
        Assert.Equal("null", output.ToString());
    }

    private static JsonSerializerSettings GetSettings()
    {
        return new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            Formatting = Newtonsoft.Json.Formatting.Indented,
            Converters =
            {
                new SchemaIdentifierConverter(),
                new ObjectIdentifierConverter(),
                new ColumnIdentifierConverter(),
                new IdentifierConverter(),
                new DatabaseComponentDictConverter(),
                new DatabaseObjectDictConverter(),
                new StringEnumConverter<ReferentialAction>(),
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
