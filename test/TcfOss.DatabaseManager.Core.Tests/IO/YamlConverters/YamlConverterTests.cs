using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.IO.YamlConverters;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace TcfOss.DatabaseManager.Core.Tests.IO.YamlConverters;

public class YamlConverterTests
{
    [Theory]
    [InlineData("catalog", typeof(CatalogIdentifier))]
    [InlineData("schema", typeof(SchemaIdentifier))]
    [InlineData("file", typeof(FileInfo))]
    [InlineData("enum", typeof(ReferentialAction))]
    public void Accepts_RecognizesSupportedTypeOnly(string kind, Type supportedType)
    {
        IYamlTypeConverter converter = CreateConverter(kind);

        Assert.True(converter.Accepts(supportedType));
        Assert.False(converter.Accepts(typeof(string)));
        Assert.False(converter.Accepts(typeof(object)));
        Assert.False(converter.Accepts(typeof(int)));
    }

    [Theory]
    [InlineData("catalog", typeof(CatalogIdentifier))]
    [InlineData("schema", typeof(SchemaIdentifier))]
    [InlineData("file", typeof(FileInfo))]
    [InlineData("enum", typeof(ReferentialAction))]
    public void WriteYaml_Null_EmitsNullScalar(string kind, Type supportedType)
    {
        IYamlTypeConverter converter = CreateConverter(kind);
        var emitter = new RecordingEmitter();

        converter.WriteYaml(emitter, null, supportedType, (_, _) => throw new InvalidOperationException("Unexpected serializer delegation."));

        var scalar = Assert.IsType<Scalar>(Assert.Single(emitter.Events));
        Assert.Equal("null", scalar.Value);
    }

    [Theory]
    [InlineData("catalog", QuoteStyle.None, "catalog")]
    [InlineData("catalog", QuoteStyle.Ansi, "\"catalog\"")]
    [InlineData("catalog", QuoteStyle.Backticks, "`catalog`")]
    [InlineData("catalog", QuoteStyle.Brackets, "[catalog]")]
    [InlineData("schema", QuoteStyle.None, "catalog.schema")]
    [InlineData("schema", QuoteStyle.Ansi, "\"catalog\".\"schema\"")]
    [InlineData("schema", QuoteStyle.Backticks, "`catalog`.`schema`")]
    [InlineData("schema", QuoteStyle.Brackets, "[catalog].[schema]")]
    public void WriteYaml_Identifier_PreservesQualificationAndQuoting(string kind, QuoteStyle quoteStyle, string expected)
    {
        var catalog = new CatalogIdentifier("catalog", quoteStyle);
        object value = kind == "catalog" ? catalog : new SchemaIdentifier("schema", catalog, quoteStyle);
        IYamlTypeConverter converter = CreateConverter(kind);
        var emitter = new RecordingEmitter();

        converter.WriteYaml(emitter, value, value.GetType(), (_, _) => throw new InvalidOperationException("Unexpected serializer delegation."));

        var scalar = Assert.IsType<Scalar>(Assert.Single(emitter.Events));
        Assert.Equal(expected, scalar.Value);
    }

    [Theory]
    [InlineData("report.xml")]
    [InlineData("reports with spaces/input.sql")]
    public void WriteYaml_FileInfo_EmitsFullPath(string path)
    {
        var converter = new YamlFileInfoConverter();
        var emitter = new RecordingEmitter();

        converter.WriteYaml(emitter, new FileInfo(path), typeof(FileInfo), (_, _) => throw new InvalidOperationException("Unexpected serializer delegation."));

        var scalar = Assert.IsType<Scalar>(Assert.Single(emitter.Events));
        Assert.Equal(Path.GetFullPath(path), scalar.Value);
    }

    [Theory]
    [InlineData(false, "CASCADE")]
    [InlineData(true, "SET NULL")]
    public void WriteYaml_StringEnum_EmitsSqlText(bool setNull, string expected)
    {
        var converter = new YamlStringEnumConverter<ReferentialAction>();
        ReferentialAction value = setNull ? ReferentialAction.SetNull : ReferentialAction.Cascade;
        var emitter = new RecordingEmitter();

        converter.WriteYaml(emitter, value, typeof(ReferentialAction), (_, _) => throw new InvalidOperationException("Unexpected serializer delegation."));

        var scalar = Assert.IsType<Scalar>(Assert.Single(emitter.Events));
        Assert.Equal(expected, scalar.Value);
    }

    [Theory]
    [InlineData("catalog", typeof(CatalogIdentifier))]
    [InlineData("schema", typeof(SchemaIdentifier))]
    [InlineData("file", typeof(FileInfo))]
    [InlineData("enum", typeof(ReferentialAction))]
    public void WriteYaml_WrongValueType_ThrowsWithoutEmitting(string kind, Type supportedType)
    {
        IYamlTypeConverter converter = CreateConverter(kind);
        var emitter = new RecordingEmitter();

        if (kind == "file")
        {
            Assert.Throws<InvalidCastException>(() => converter.WriteYaml(emitter, 42, supportedType, (_, _) => throw new InvalidOperationException("Unexpected serializer delegation.")));
        }
        else
        {
            var exception = Assert.Throws<InvalidOperationException>(() => converter.WriteYaml(emitter, 42, supportedType, (_, _) => throw new InvalidOperationException("Unexpected serializer delegation.")));
            Assert.Contains(nameof(Int32), exception.Message);
        }
        Assert.Empty(emitter.Events);
    }

    [Theory]
    [InlineData("report.xml", "report.xml")]
    [InlineData("'reports with spaces/input.sql'", "reports with spaces/input.sql")]
    [InlineData("\"reports with spaces/input.sql\"", "reports with spaces/input.sql")]
    public void ReadYaml_FileInfo_DeserializesScalar(string yaml, string expectedPath)
    {
        var deserializer = new DeserializerBuilder().WithTypeConverter(new YamlFileInfoConverter()).Build();

        var actual = deserializer.Deserialize<FileInfo>(yaml);

        Assert.Equal(Path.GetFullPath(expectedPath), actual.FullName);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    public void ReadYaml_FileInfo_RejectsNonScalar(string yaml)
    {
        var deserializer = new DeserializerBuilder().WithTypeConverter(new YamlFileInfoConverter()).Build();

        Assert.ThrowsAny<YamlException>(() => deserializer.Deserialize<FileInfo>(yaml));
    }

    private static IYamlTypeConverter CreateConverter(string kind)
    {
        return kind switch
        {
            "catalog" => new YamlCatalogIdentifierConverter(),
            "schema" => new YamlSchemaIdentifierConverter(),
            "file" => new YamlFileInfoConverter(),
            "enum" => new YamlStringEnumConverter<ReferentialAction>(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private sealed class RecordingEmitter : IEmitter
    {
        public List<ParsingEvent> Events { get; } = [];

        public void Emit(ParsingEvent @event)
        {
            Events.Add(@event);
        }
    }
}