using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.MsSql.Configuration;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.Configuration;

public class MsConfigLoaderTests
{
    [Theory]
    [InlineData(null, "16.0.0")]
    [InlineData("", "16.0.0")]
    [InlineData(" ", "16.0.0")]
    [InlineData("not-a-version", "16.0.0")]
    [InlineData("15.0.1000", "15.0.1000")]
    [InlineData("16.0.1000.6", "16.0.1000.6")]
    public void MsConfigurationBuilder_ConnectionFailure_ResolvesFallbackVersion(string? configuredVersion, string expectedVersion)
    {
        string rootDirectory = Path.GetFullPath(Path.GetTempPath());
        var builder = new MsConfigurationBuilder();
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

        Assert.Equal(SqlDialect.MsSql, config.Dialect);
        Assert.Equal(rootDirectory, config.ProjectDirectory);
        Assert.Equal("master", config.Catalog.Name);
        Assert.Equal(1, config.DatabaseCredentials.Port);
        Assert.Equal(QuoteStyle.Brackets, config.QuoteStyle);
        Assert.False(config.DatabaseAvailable);
        Assert.Equal(new Version(expectedVersion), config.Version);
    }

    [Fact]
    public void Basic_Config_Loads()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        var config = LoadConfig(rawConfig);

        Assert.NotNull(config);
        Assert.Equal("mydb", config.Catalog.Name);
        Assert.Equal(QuoteStyle.Brackets, config.QuoteStyle);
        var expectedRootPath = Path.GetFullPath("/home/username/database");
        Assert.Equal(expectedRootPath, config.ProjectDirectory);
        Assert.Equal(2, config.Schemas.Count);

        var schemaValues = config.Schemas.Values.ToList();
        Assert.Equal("dbo", schemaValues[0].SchemaName.Name);
        Assert.Contains("excluded_table", schemaValues[0].ExcludeDatabaseObjectNames);
        Assert.Equal("reporting", schemaValues[1].SchemaName.Name);

        Assert.True(Path.IsPathRooted(config.ProjectDirectory));
        Assert.True(Path.IsPathRooted(schemaValues[0].RootPath));
        Assert.True(Path.IsPathRooted(schemaValues[1].RootPath));
        var expectedPath1 = Path.Combine(expectedRootPath, "dbo");
        Assert.StartsWith(expectedPath1, schemaValues[0].RootPath);
        var expectedPath2 = Path.Combine(expectedRootPath, "subdir", "reporting");
        Assert.StartsWith(expectedPath2, schemaValues[1].RootPath);

        Assert.Equal("sqlserver", config.DatabaseCredentials.Host);
        Assert.Equal(1433, config.DatabaseCredentials.Port);
        Assert.Equal("testuser", config.DatabaseCredentials.Username);
        Assert.Equal("testpassword", config.DatabaseCredentials.Password);
    }

    [Fact]
    public void FillsDefault_QuoteStyle()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.QuoteStyle = null;
        var config = LoadConfig(rawConfig);

        Assert.Equal(QuoteStyle.Brackets, config.QuoteStyle);
    }

    [Fact]
    public void Throws_On_Missing_Catalog()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.MsSql,
            Schemas =
            [
                new ConfigParsing.SchemaMapping { SchemaName = "dbo", RootPath = "dbo" }
            ],
        };

        Assert.Throws<ConfigurationException.MissingFieldException>(() => LoadConfig(rawConfig));
    }

    [Fact]
    public void Absolutizes_Relative_Project_Directory()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Catalog = "mydb",
            Dialect = SqlDialect.MsSql,
            ProjectDirectory = "relative/path/to/project",
            Schemas =
            [
                new ConfigParsing.SchemaMapping { SchemaName = "dbo", RootPath = "dbo" }
            ],
        };
        var config = LoadConfig(rawConfig);

        Assert.Equal(Path.GetFullPath("/home/username/database/relative/path/to/project"), config.ProjectDirectory);
        Assert.StartsWith(Path.GetFullPath("/home/username/database/relative/path/to/project/dbo"), config.Schemas.Values.First().RootPath);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("15.0.1000", false)]
    [InlineData("15.0.1000", true)]
    [InlineData("not-a-version", false)]
    [InlineData("not-a-version", true)]
    public void Version_UsesResolvedOtherInformation(string? configuredVersion, bool databaseAvailable)
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.Version = configuredVersion;
        var version = new Version(16, 0, 1000);
        var otherInfo = MsGetOtherData.GetData(rawConfig, databaseAvailable, version);

        var config = LoadConfig(rawConfig, otherInfo);

        Assert.Same(version, config.Version);
        Assert.Equal(databaseAvailable, config.DatabaseAvailable);
    }

    [Fact]
    public void Throws_On_Missing_Credentials_Key()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        var otherInfo = new Dictionary<string, object>();

        Assert.Throws<KeyNotFoundException>(() => LoadConfig(rawConfig, otherInfo));
    }

    [Fact]
    public void Throws_On_Empty_SchemaRootPath()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.MsSql,
            Schemas =
            [
                new ConfigParsing.SchemaMapping { SchemaName = "dbo", RootPath = "" }
            ],
        };

        Assert.Throws<ConfigurationException.MissingFieldException>(() => LoadConfig(rawConfig));
    }

    [Fact]
    public void Throws_On_Empty_Schema_Name()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.MsSql,
            Schemas =
            [
                new ConfigParsing.SchemaMapping { SchemaName = "", RootPath = "dbo" }
            ],
        };

        Assert.Throws<ConfigurationException.MissingFieldException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData("DatabaseAvailable")]
    [InlineData("Version")]
    public void MissingResolvedInformation_Throws(string key)
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        var otherInfo = MsGetOtherData.GetData(MsGetOtherData.DefaultCredentials);
        otherInfo.Remove(key);

        Assert.Throws<KeyNotFoundException>(() => LoadConfig(rawConfig, otherInfo));
    }

    [Fact]
    public void DatabaseAvailable_Reflects_OtherInfo_Value()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        var otherInfo = MsGetOtherData.GetData(MsGetOtherData.DefaultCredentials, databaseAvailable: true);
        var config = LoadConfig(rawConfig, otherInfo);

        Assert.True(config.DatabaseAvailable);
    }

    [Fact]
    public void ConnectionString_Uses_SqlAuth_When_Username_Set()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        var config = LoadConfig(rawConfig);

        Assert.Contains("User ID=testuser", config.ConnectionString);
        Assert.DoesNotContain("Integrated Security", config.ConnectionString);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConnectionString_DelegatesToCredentials(bool integratedSecurity)
    {
        var rawConfig = integratedSecurity ? TestDataRawConfig.MsTestRawConfigWindowsAuth : TestDataRawConfig.MsTestRawConfig;

        var config = LoadConfig(rawConfig);

        Assert.Equal(config.DatabaseCredentials.ConnectionString, config.ConnectionString);
    }

    [Fact]
    public void ConnectionString_Uses_IntegratedSecurity_When_No_Username()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfigWindowsAuth;
        var config = LoadConfig(rawConfig);

        Assert.Contains("Integrated Security=True", config.ConnectionString);
        Assert.DoesNotContain("User ID", config.ConnectionString);
    }

    [Fact]
    public void DataSource_DefaultPort_Is_Hostname_Only()
    {
        var credentials = new MsDatabaseCredentials { Username = "testuser", Host = "sqlserver", Port = 1433, Encrypt = true, TrustServerCertificate = false, ConnectionTimeout = 30 };
        Assert.Equal("sqlserver", credentials.DataSource);
    }

    [Fact]
    public void DataSource_NonDefaultPort_Includes_Port()
    {
        var credentials = new MsDatabaseCredentials { Username = "testuser", Host = "sqlserver", Port = 1434, Encrypt = true, TrustServerCertificate = false, ConnectionTimeout = 30 };
        Assert.Equal("sqlserver,1434", credentials.DataSource);
    }

    [Fact]
    public void DataSource_NamedInstance_Uses_Backslash_Notation()
    {
        var credentials = new MsDatabaseCredentials { Username = "testuser", Host = "sqlserver", Instance = "SQLEXPRESS", Encrypt = true, TrustServerCertificate = false, ConnectionTimeout = 30 };
        Assert.Equal(@"sqlserver\SQLEXPRESS", credentials.DataSource);
    }

    [Fact]
    public void Missing_Credentials_Throws()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.Credentials = null;

        var ex = Assert.Throws<ConfigurationException.MissingFieldException>(() => BuildConfig(rawConfig));
        Assert.Contains("Credentials", ex.Message);
    }

    [Fact]
    public void Missing_Credentials_Port_Supplies_Default()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.Credentials!.Port = null;

        var config = BuildConfig(rawConfig);
        Assert.Equal(1433, config.DatabaseCredentials.Port);
    }

    [Theory]
    [InlineData("notanumber")]
    [InlineData("-1")]
    [InlineData("65536")]
    public void Invalid_Port_Throws(string portString)
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.Credentials!.Port = portString;

        var ex = Assert.Throws<ConfigurationException.InvalidPortException>(() => BuildConfig(rawConfig));
        Assert.Contains(portString, ex.Message);
    }

    [Fact]
    public void EncryptOverride_Applied()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.Credentials!.Encrypt = false;

        var config = BuildConfig(rawConfig);
        Assert.False(config.DatabaseCredentials.Encrypt);
    }

    [Fact]
    public void TrustServerCertificateOverride_Applied()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.Credentials!.TrustServerCertificate = true;

        var config = BuildConfig(rawConfig);
        Assert.True(config.DatabaseCredentials.TrustServerCertificate);
    }

    private static MsConfig LoadConfig(ConfigParsing.Config rawConfig, Dictionary<string, object>? otherInfo = null)
    {
        otherInfo ??= MsGetOtherData.GetData(rawConfig);
        return new MsConfigLoader(new LoggerFactory().CreateLogger<MsConfigLoader>()).LoadConfig(Path.GetFullPath("/home/username/database"), rawConfig, otherInfo);
    }

    private static MsConfig BuildConfig(ConfigParsing.Config rawConfig)
    {
        var testLogger = NullLogger.Instance;
        var rootPath = rawConfig.ProjectDirectory ?? "/default/path/";

        var builder = new MsConfigurationBuilder();

        return builder.BuildConfiguration(rootPath, rawConfig, new EnvironmentVariableReader(), false, testLogger);
    }
}
