using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MariaDb.Configuration;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.Tests.Configuration;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MariaDb.Tests.Configuration;

public class MaConfigLoaderTests
{
    [Fact]
    public void MaConfigurationBuilder_SuppliesMariaDbDefaultsWithoutHosting()
    {
        var rawConfig = new MaConfigurationBuilder().GetDefaultConfig(Path.GetTempPath());

        Assert.Equal(SqlDialect.MariaDb, rawConfig.Dialect);
        Assert.Equal(QuoteStyle.Backticks, rawConfig.QuoteStyle);
        Assert.Equal("def", rawConfig.Catalog);
        Assert.Equal("3306", rawConfig.Credentials!.Port);
        Assert.Equal("INFORMATION_SCHEMA", Assert.Single(rawConfig.Schemas).SchemaName);
    }

    private static MyConfig LoadConfig(ConfigParsing.Config rawConfig, Dictionary<string, object>? otherInfo = null)
    {
        otherInfo ??= MaGetOtherData.GetData(rawConfig);
        return new MaConfigLoader(new LoggerFactory().CreateLogger<MaConfigLoader>()).LoadConfig(Path.GetFullPath("/home/username/database"), rawConfig, otherInfo);
    }

    [Theory]
    [InlineData(null, "11.8.2")]
    [InlineData("", "11.8.2")]
    [InlineData(" ", "11.8.2")]
    [InlineData("invalid", "11.8.2")]
    [InlineData("10.11.13", "10.11.13")]
    [InlineData("11.8.1", "11.8.1")]
    public void ConfigurationBuilder_ConnectionFailure_ResolvesFallbackVersion(string? configuredVersion, string expectedVersion)
    {
        string rootDirectory = Path.GetFullPath(Path.GetTempPath());
        var builder = new MaConfigurationBuilder();
        var rawConfig = builder.GetDefaultConfig(rootDirectory);
        rawConfig.Version = configuredVersion;
        rawConfig.Credentials = new ConfigParsing.Credentials
        {
            Hostname = "127.0.0.1",
            Port = "1",
            Username = "testuser",
            Password = "testpassword",
            ConnectionTimeout = 1,
        };

        var config = builder.BuildConfiguration(rootDirectory, rawConfig, new EnvironmentVariableReader(), false, NullLogger.Instance);

        Assert.Equal(new Version(expectedVersion), config.Version);
        Assert.False(config.DatabaseAvailable);
        Assert.Equal(SqlDialect.MariaDb, config.Dialect);
    }

    [Theory]
    [InlineData(null, "10.11.13", false)]
    [InlineData(null, "11.8.2", true)]
    [InlineData("11.0.0", "10.11.13", true)]
    [InlineData("10.0.0", "11.8.2", false)]
    [InlineData("invalid", "10.11.13", false)]
    [InlineData("invalid", "11.8.2", true)]
    public void Version_UsesResolvedOtherInformation(string? configuredVersion, string resolvedVersion, bool databaseAvailable)
    {
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig();
        rawConfig.Dialect = SqlDialect.MariaDb;
        rawConfig.Version = configuredVersion;
        var version = new Version(resolvedVersion);
        var otherInfo = MaGetOtherData.GetData(rawConfig, version, databaseAvailable);

        var config = LoadConfig(rawConfig, otherInfo);

        Assert.Same(version, config.Version);
        Assert.Equal(databaseAvailable, config.DatabaseAvailable);
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("DatabaseAvailable")]
    public void MissingResolvedInformation_Throws(string key)
    {
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig();
        var otherInfo = MaGetOtherData.GetData(rawConfig);
        otherInfo.Remove(key);

        Assert.Throws<KeyNotFoundException>(() => LoadConfig(rawConfig, otherInfo));
    }

    [Fact]
    public void Credentials_ConnectionTimeout_Uses_Specified_Value()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.Credentials!.ConnectionTimeout = 60;
        var config = LoadConfig(rawConfig);

        Assert.Equal<uint>(60, config.DatabaseCredentials.ConnectionTimeout);
    }

    [Fact]
    public void Credentials_ConnectionTimeout_Uses_Default_When_Not_Set()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.Credentials!.ConnectionTimeout = null;
        var config = LoadConfig(rawConfig);

        Assert.Equal<uint>(30, config.DatabaseCredentials.ConnectionTimeout);
    }

    [Fact]
    public void AttributeDefaults_NonOverrides_MariaDb()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfigSillyDefaults;
        rawConfig.Dialect = SqlDialect.MariaDb;
        var config = new MaConfigLoader(new LoggerFactory().CreateLogger<MaConfigLoader>()).LoadConfig("/home/username/database", rawConfig, MaGetOtherData.GetData(rawConfig));

        Assert.Equal<uint?>(3, config.AttributeDefaults.SignedIntWidth);
        Assert.Equal<uint?>(10, config.AttributeDefaults.UnsignedIntWidth);
        Assert.Equal<uint?>(20, config.AttributeDefaults.SignedBigIntWidth);
        Assert.Equal<uint?>(25, config.AttributeDefaults.UnsignedBigIntWidth);
        Assert.Equal<uint?>(9, config.AttributeDefaults.SignedMediumIntWidth);
        Assert.Equal<uint?>(8, config.AttributeDefaults.UnsignedMediumIntWidth);
        Assert.Equal<uint?>(6, config.AttributeDefaults.SignedSmallIntWidth);
        Assert.Equal<uint?>(5, config.AttributeDefaults.UnsignedSmallIntWidth);
        Assert.Equal<uint?>(4, config.AttributeDefaults.SignedTinyIntWidth);
        Assert.Equal<uint?>(3, config.AttributeDefaults.UnsignedTinyIntWidth);
        Assert.Equal(new NumericLength.PrecisionScale(10, 0), config.AttributeDefaults.DecimalPrecision);
        Assert.Null(config.AttributeDefaults.FloatPrecision);
        Assert.Null(config.AttributeDefaults.DoublePrecision);
        Assert.Equal<uint?>(4, config.AttributeDefaults.YearPrecision);
    }

    [Fact]
    public void AttributeDefaults_OverrideWithNull_MariaDb()
    {
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig();
        rawConfig.Dialect = SqlDialect.MariaDb;
        rawConfig.AttributeDefaults = new()
        {
            SignedIntWidth = null,
            UnsignedIntWidth = 4,
        };

        var config = new MaConfigLoader(new LoggerFactory().CreateLogger<MaConfigLoader>()).LoadConfig("/home/username/database", rawConfig, MaGetOtherData.GetData(rawConfig));

        Assert.Null(config.AttributeDefaults.SignedIntWidth);
        Assert.Equal<uint?>(4, config.AttributeDefaults.UnsignedIntWidth);
    }
}
