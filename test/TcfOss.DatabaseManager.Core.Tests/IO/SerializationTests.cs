using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Resources;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Tests.Resources;

namespace TcfOss.DatabaseManager.Core.Tests.IO;

public class SerializationTests
{
    private static readonly string s_text = """
        SELECT
            -- comment
            Column1,
            Column2
        FROM Schema1.Table1
        WHERE Column1 = 'Value'
        """;

    protected static TestView GetView()
    {
        var tp = new TextParser(new GenericLexer(), new Parser());
        var lines = s_text.Split("\n");
        lines[0] = lines[0] + " ";
        lines[2] = lines[2] + " ";
        var text = string.Join("\n", lines);
        var view = new TestView(
            new ObjectIdentifier("View1", new SchemaIdentifier("Schema1", new CatalogIdentifier("def"))),
            (Select)tp.ParseText(text).First()
        )
        {
            RawBodyText = text
        };
        return view;
    }

    [Fact]
    public void DefaultSettings()
    {
        var json = Serialization.ToJson(GetView());

        Assert.Equal(TestText.SerializationTests_FullViewJson.Trim().ReplaceLineEndings("\n"), json.ReplaceLineEndings("\n"), ignoreWhiteSpaceDifferences: true);
    }

    [Fact]
    public void OmitMetaData()
    {
        var settings = new SerializationSettings()
        {
            OmitMetaData = true,
            OmitPreNonSql = false,
            OmitRawText = false,
        };
        var json = Serialization.ToJson(GetView(), settings);

        Assert.DoesNotContain("\"Meta\":", json);
        Assert.Contains("\"PreNonSql\":", json);
        Assert.Contains("\"RawBodyText\":", json);
        Assert.Contains("\"Source\":", json);
    }

    [Fact]
    public void OmitPreNonSql()
    {
        var settings = new SerializationSettings()
        {
            OmitMetaData = false,
            OmitPreNonSql = true,
            OmitRawText = false,
        };
        var json = Serialization.ToJson(GetView(), settings);

        Assert.Equal(TestText.SerializationTests_JsonWithPreNonSql.Trim().ReplaceLineEndings("\n"), json.ReplaceLineEndings("\n"), ignoreWhiteSpaceDifferences: true);
    }

    [Fact]
    public void OmitRawText()
    {
        var settings = new SerializationSettings()
        {
            OmitMetaData = false,
            OmitPreNonSql = false,
            OmitRawText = true,
        };
        var json = Serialization.ToJson(GetView(), settings);

        Assert.Contains("\"Meta\":", json);
        Assert.Contains("\"PreNonSql\":", json);
        Assert.DoesNotContain("\"RawBodyText\":", json);
        Assert.Contains("\"Source\":", json);
    }

    [Fact]
    public void OmitSourceRef()
    {
        var settings = new SerializationSettings()
        {
            OmitMetaData = false,
            OmitPreNonSql = false,
            OmitRawText = false,
            OmitSourceRef = true
        };
        var json = Serialization.ToJson(GetView(), settings);

        Assert.Contains("\"Meta\":", json);
        Assert.Contains("\"PreNonSql\":", json);
        Assert.Contains("\"RawBodyText\":", json);
        Assert.DoesNotContain("\"Source\":", json);
    }

    protected record TestView(ObjectIdentifier Name, Select Body) : View(Name, Body)
    {
        public override CreateView ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
        {
            return new CreateView(Name.ToObjectName(includeSchema ? 2 : 1), Body);
        }
    }

    [Fact]
    public void FromYaml_ValidConfig()
    {
        var yaml = """
        RequiredValue: "Hello"
        OptionalValue: "World"
        """;

        using var reader = new StringReader(yaml);
        var config = Serialization.ParseConfig<TestConfig>(reader, "test.yaml");

        Assert.Equal("Hello", config.RequiredValue);
        Assert.Equal("World", config.OptionalValue);
        Assert.Equal("Default", config.DefaultedOptionalValue);
    }

    [Fact]
    public void FromYaml_UnknownFieldThrows()
    {
        var yaml = """
        RequiredValue: "Hello"
        OptionalValue: "World"
        UnknownValue: 123
        """;

        var message = Assert.Throws<ConfigParseException.WithText>(() =>
        {
            _ = Serialization.ParseConfig<TestConfig>(yaml, "test.yaml");
        });
        var expectedMessage = string.Format(ErrorMessages.Err_Conf_UnknownField, "UnknownValue");
        var expected = string.Format(
            TestText.BadConfig_Error_Template,
            expectedMessage,
            "test.yaml",
            3,
            1,
            "UnknownValue: 123",
            "^");
        Assert.Equal(expected, message.Message);
    }

    [Fact]
    public void FromYaml_ExplicitNullabilityThrows()
    {
        var yaml = """
        RequiredValue: null
        OptionalValue: "World
        """;

        var message = Assert.Throws<ConfigParseException.WithText>(() =>
        {
            _ = Serialization.ParseConfig<TestConfig>(yaml, "test.yaml");
        });

        var expectedMessage = string.Format(ErrorMessages.Err_Conf_InvalidNull, "RequiredValue");
        var expected = string.Format(
            TestText.BadConfig_Error_Template,
            expectedMessage,
            "test.yaml",
            1,
            16,
            "RequiredValue: null",
            "               ^");

        Assert.Equal(expected, message.Message);
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
    protected class TestConfig
    {
        public required string RequiredValue { get; init; }
        public string? OptionalValue { get; init; }
        public string DefaultedOptionalValue { get; init; } = "Default";
    }
}
