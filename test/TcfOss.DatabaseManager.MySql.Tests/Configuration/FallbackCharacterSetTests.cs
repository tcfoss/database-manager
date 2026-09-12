using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;

namespace TcfOss.DatabaseManager.MySql.Tests.Configuration;

public class FallbackCharacterSetTests
{
    private readonly FallbackDatabaseDefaultLoader _loader = new(new LoggerFactory().CreateLogger<FallbackDatabaseDefaultLoader>());
    private static readonly CatalogIdentifier s_catalogId = new("def", QuoteStyle.Backticks);
    private static readonly SchemaIdentifier s_schemaId = new("test", s_catalogId);

    [Theory]
    [InlineData(SqlDialect.MySql, 5, 7, "latin1", "latin1_swedish_ci")]
    [InlineData(SqlDialect.MySql, 8, 0, "utf8mb4", "utf8mb4_0900_ai_ci")]
    [InlineData(SqlDialect.MariaDb, 10, 0, "latin1", "latin1_swedish_ci")]
    [InlineData(SqlDialect.MariaDb, 10, 1, "utf8mb4", "utf8mb4_general_ci")]
    [InlineData(SqlDialect.MariaDb, 10, 9, "utf8mb4", "utf8mb4_general_ci")]
    [InlineData(SqlDialect.MariaDb, 11, 4, "utf8mb4", "utf8mb4_general_ci")]
    [InlineData(SqlDialect.MariaDb, 11, 5, "utf8mb4", "utf8mb4_uca1400_ai_ci")]
    public void Server_Defaults(SqlDialect dialect, int major, int minor, string expectedCharset, string expectedCollation)
    {
        var defaults = _loader.GetServerDefaults(dialect, new Version(major, minor));

        Assert.Equal(expectedCharset, defaults.CharacterSet);
        Assert.Equal(expectedCollation, defaults.Collation);
        Assert.Equal("InnoDB", defaults.Engine);
    }

    [Theory]
    [InlineData(SqlDialect.MySql, 5, 7, "latin1", "latin1_swedish_ci")]
    [InlineData(SqlDialect.MySql, 8, 0, "utf8mb4", "utf8mb4_0900_ai_ci")]
    [InlineData(SqlDialect.MariaDb, 10, 0, "latin1", "latin1_swedish_ci")]
    [InlineData(SqlDialect.MariaDb, 10, 1, "utf8mb4", "utf8mb4_general_ci")]
    [InlineData(SqlDialect.MariaDb, 10, 9, "utf8mb4", "utf8mb4_general_ci")]
    [InlineData(SqlDialect.MariaDb, 11, 4, "utf8mb4", "utf8mb4_general_ci")]
    [InlineData(SqlDialect.MariaDb, 11, 5, "utf8mb4", "utf8mb4_uca1400_ai_ci")]
    public void Schema_Defaults_Equal_Defaults(SqlDialect dialect, int major, int minor, string expectedCharset, string expectedCollation)
    {
        var serverDefaults = _loader.GetServerDefaults(dialect, new Version(major, minor));
        var schemaDefaults = _loader.GetSchemaDefaults(s_schemaId.Name, s_catalogId.Name, serverDefaults);

        Assert.Equal(expectedCharset, schemaDefaults.CharacterSet);
        Assert.Equal(expectedCollation, schemaDefaults.Collation);
        Assert.Equal("InnoDB", schemaDefaults.Engine);

        Assert.Equal(serverDefaults.CharacterSet, schemaDefaults.CharacterSet);
        Assert.Equal(serverDefaults.Collation, schemaDefaults.Collation);
        Assert.Equal(serverDefaults.Engine, schemaDefaults.Engine);
    }

    [Fact]
    public void CharacterSetsLoaded_MySql_8()
    {
        var charsets = _loader.GetCharacterSets(SqlDialect.MySql, new Version(8, 0));

        Assert.NotNull(charsets);
        Assert.Equal(3, charsets.Count);

        Assert.True(charsets.ContainsKey("utf8mb4"));
        var utf8mb4Sets = charsets["utf8mb4"];
        Assert.Contains("utf8mb4_0900_ai_ci", utf8mb4Sets.Collations);
        Assert.Contains("utf8mb4_bin", utf8mb4Sets.Collations);
        Assert.Equal(2, utf8mb4Sets.Collations.Count);

        Verify_Latin1_Ascii(charsets);
    }

    [Fact]
    public void CharacterSetsLoaded_MySql_5()
    {
        var charsets = _loader.GetCharacterSets(SqlDialect.MySql, new Version(5, 7));

        Assert.NotNull(charsets);
        Assert.Equal(4, charsets.Count);
        Assert.True(charsets.ContainsKey("utf8mb4"));
        var utf8mb4Sets = charsets["utf8mb4"];
        Assert.Contains("utf8mb4_general_ci", utf8mb4Sets.Collations);
        Assert.Contains("utf8mb4_unicode_ci", utf8mb4Sets.Collations);
        Assert.Contains("utf8mb4_bin", utf8mb4Sets.Collations);
        Assert.Equal(3, utf8mb4Sets.Collations.Count);

        Assert.True(charsets.ContainsKey("utf8"));
        var utf8Sets = charsets["utf8"];
        Assert.Contains("utf8_general_ci", utf8Sets.Collations);
        Assert.Contains("utf8_unicode_ci", utf8Sets.Collations);
        Assert.Contains("utf8_bin", utf8Sets.Collations);
        Assert.Equal(3, utf8Sets.Collations.Count);

        Verify_Latin1_Ascii(charsets);
    }

    [Theory]
    [InlineData(11, 5)]
    [InlineData(11, 0)]
    public void CharacterSetsLoaded_MariaDb_10_10_Plus(int major, int minor)
    {
        var charsets = _loader.GetCharacterSets(SqlDialect.MariaDb, new Version(major, minor));

        Assert.NotNull(charsets);
        Assert.Equal(4, charsets.Count);

        Assert.True(charsets.ContainsKey("utf8mb4"));
        var utf8mb4Sets = charsets["utf8mb4"];
        Assert.Contains("utf8mb4_uca1400_ai_ci", utf8mb4Sets.Collations);
        Assert.Contains("utf8mb4_general_ci", utf8mb4Sets.Collations);
        Assert.Contains("utf8mb4_unicode_ci", utf8mb4Sets.Collations);
        Assert.Contains("utf8mb4_bin", utf8mb4Sets.Collations);
        Assert.Equal(4, utf8mb4Sets.Collations.Count);

        Assert.True(charsets.ContainsKey("utf8mb3"));
        var utf8mb3Sets = charsets["utf8mb3"];
        Assert.Contains("utf8mb3_general_ci", utf8mb3Sets.Collations);
        Assert.Contains("utf8mb3_unicode_ci", utf8mb3Sets.Collations);
        Assert.Contains("utf8mb3_bin", utf8mb3Sets.Collations);
        Assert.Equal(3, utf8mb3Sets.Collations.Count);

        Verify_Latin1_Ascii(charsets);
    }

    [Fact]
    public void CharacterSetsLoaded_MariaDb_10_5()
    {
        var charsets = _loader.GetCharacterSets(SqlDialect.MariaDb, new Version(10, 5));

        Assert.NotNull(charsets);
        Assert.Equal(4, charsets.Count);

        Assert.True(charsets.ContainsKey("utf8mb4"));
        var utf8mb4Sets = charsets["utf8mb4"];
        Assert.Contains("utf8mb4_general_ci", utf8mb4Sets.Collations);
        Assert.Contains("utf8mb4_unicode_ci", utf8mb4Sets.Collations);
        Assert.Contains("utf8mb4_bin", utf8mb4Sets.Collations);
        Assert.Equal(3, utf8mb4Sets.Collations.Count);

        Assert.True(charsets.ContainsKey("utf8mb3"));
        var utf8mb3Sets = charsets["utf8mb3"];
        Assert.Contains("utf8mb3_general_ci", utf8mb3Sets.Collations);
        Assert.Contains("utf8mb3_unicode_ci", utf8mb3Sets.Collations);
        Assert.Contains("utf8mb3_bin", utf8mb3Sets.Collations);
        Assert.Equal(3, utf8mb3Sets.Collations.Count);

        Verify_Latin1_Ascii(charsets);
    }

    private static void Verify_Latin1_Ascii(Dictionary<string, CharacterSetSpec> characterSets)
    {
        Assert.True(characterSets.ContainsKey("latin1"));
        var latin1Sets = characterSets["latin1"];
        Assert.Contains("latin1_swedish_ci", latin1Sets.Collations);
        Assert.Contains("latin1_bin", latin1Sets.Collations);
        Assert.Equal(2, latin1Sets.Collations.Count);

        Assert.True(characterSets.ContainsKey("ascii"));
        var asciiSets = characterSets["ascii"];
        Assert.Contains("ascii_general_ci", asciiSets.Collations);
        Assert.Contains("ascii_bin", asciiSets.Collations);
        Assert.Equal(2, asciiSets.Collations.Count);
    }
}
