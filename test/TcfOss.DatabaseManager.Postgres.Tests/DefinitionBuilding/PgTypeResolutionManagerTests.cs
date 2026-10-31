using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DefinitionBuilding;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.DefinitionBuilding;

public class PgTypeResolutionManagerTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("mood", "app", "mood")]
    [InlineData("MOOD", "app", "mood")]
    [InlineData("public.mood", "public", "mood")]
    [InlineData("catalog.app.mood", "app", "mood")]
    [InlineData("\"App\".\"Mood\"", "App", "Mood")]
    public void Resolve_HonorsSearchPathAndQuotedNames(string sql, string expectedSchema, string expectedName)
    {
        var config = CreateConfig();
        var names = new Dictionary<ObjectHandle, ObjectIdentifier>
        {
            [new ObjectHandle("catalog", "app", "mood")] = ObjectIdentifier.FromStrings("catalog", "app", "mood"),
            [new ObjectHandle("catalog", "public", "mood")] = ObjectIdentifier.FromStrings("catalog", "public", "mood"),
            [new ObjectHandle("catalog", "App", "Mood")] = ObjectIdentifier.FromStrings("catalog", "App", "Mood", QuoteStyle.Ansi),
        };
        var manager = new PgTypeResolutionManager(config, names);
        DataTypeReference reference = new PgParser().DataTypeParser.ParseDataTypeReference(GetState(sql));

        var resolved = Assert.IsType<PgDataType.Custom>(manager.Resolve(reference, new SchemaIdentifier("public", config.Catalog)));

        Assert.Equal(expectedSchema, resolved.Name.Schema.Name);
        Assert.Equal(expectedName, resolved.Name.Name);
    }

    [Fact]
    public void Resolve_PreservesBuiltInType()
    {
        var config = CreateConfig();
        var manager = new PgTypeResolutionManager(config, new Dictionary<ObjectHandle, ObjectIdentifier>());
        var dataType = new DataType.Text();

        DataType resolved = manager.Resolve(dataType, new SchemaIdentifier("public", config.Catalog));

        Assert.Same(dataType, resolved);
    }

    [Fact]
    public void Resolve_RejectsMissingTypes()
    {
        var config = CreateConfig();
        var manager = new PgTypeResolutionManager(config, new Dictionary<ObjectHandle, ObjectIdentifier>());
        DataTypeReference reference = new PgParser().DataTypeParser.ParseDataTypeReference(GetState("missing"));

        Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(() => manager.Resolve(reference, new SchemaIdentifier("public", config.Catalog)));
    }

    [Theory]
    [InlineData("mood")]
    [InlineData("app.mood")]
    [InlineData("other_catalog.app.mood")]
    [InlineData("\"Mood\"")]
    public void Resolve_DoesNotGuessMissingSchemaCatalogOrQuotedName(string sql)
    {
        var config = CreateConfig() with { SearchPath = [new Identifier("absent")] };
        var names = new Dictionary<ObjectHandle, ObjectIdentifier>
        {
            [new ObjectHandle("catalog", "public", "mood")] = ObjectIdentifier.FromStrings("catalog", "public", "mood")
        };
        var manager = new PgTypeResolutionManager(config, names);
        DataTypeReference reference = new PgParser().DataTypeParser.ParseDataTypeReference(GetState(sql));

        Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(() => manager.Resolve(reference, new SchemaIdentifier("public", config.Catalog)));
    }

    [Fact]
    public void Resolve_FallsThroughAbsentSearchPathEntries()
    {
        var config = CreateConfig();
        var names = new Dictionary<ObjectHandle, ObjectIdentifier>
        {
            [new ObjectHandle("catalog", "public", "mood")] = ObjectIdentifier.FromStrings("catalog", "public", "mood")
        };
        var manager = new PgTypeResolutionManager(config, names);
        DataTypeReference reference = new PgParser().DataTypeParser.ParseDataTypeReference(GetState("mood"));

        var resolved = Assert.IsType<PgDataType.Custom>(manager.Resolve(reference, new SchemaIdentifier("other", config.Catalog)));

        Assert.Equal("public", resolved.Name.Schema.Name);
    }

    [Fact]
    public void Resolve_PreservesQuotedSearchPathSchema()
    {
        var config = CreateConfig() with { SearchPath = [new Identifier("App", QuoteStyle.Ansi)] };
        var names = new Dictionary<ObjectHandle, ObjectIdentifier>
        {
            [new ObjectHandle("catalog", "App", "mood")] = ObjectIdentifier.FromStrings("catalog", "App", "mood", QuoteStyle.Ansi)
        };
        var manager = new PgTypeResolutionManager(config, names);
        DataTypeReference reference = new PgParser().DataTypeParser.ParseDataTypeReference(GetState("mood"));

        var resolved = Assert.IsType<PgDataType.Custom>(manager.Resolve(reference, new SchemaIdentifier("public", config.Catalog)));

        Assert.Equal("App", resolved.Name.Schema.Name);
    }

    [Fact]
    public void Resolve_EmptySearchPathDoesNotImplicitlySearchOwnerSchema()
    {
        var config = CreateConfig() with { SearchPath = [] };
        var names = new Dictionary<ObjectHandle, ObjectIdentifier>
        {
            [new ObjectHandle("catalog", "public", "mood")] = ObjectIdentifier.FromStrings("catalog", "public", "mood")
        };
        var manager = new PgTypeResolutionManager(config, names);
        DataTypeReference reference = new PgParser().DataTypeParser.ParseDataTypeReference(GetState("mood"));

        Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(() => manager.Resolve(reference, new SchemaIdentifier("public", config.Catalog)));
    }

    private static PgConfig CreateConfig()
    {
        return new PgConfig
        {
            ProjectDirectory = ".",
            Catalog = new CatalogIdentifier("catalog"),
            DatabaseAvailable = false,
            ValidationSettings = new ValidationSettings(),
            QuoteStyle = QuoteStyle.Ansi,
            NameHandling = NameHandling.LowercaseUnlessQuoted,
            SearchPath = [new Identifier("app"), new Identifier("public")]
        };
    }
}
