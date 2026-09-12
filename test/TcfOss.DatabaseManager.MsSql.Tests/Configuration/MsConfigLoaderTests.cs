using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.MsSql.Configuration;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.Configuration;

public class MsConfigLoaderTests
{
    private static MsConfig LoadConfig(ConfigParsing.Config rawConfig, Dictionary<string, object>? otherInfo = null)
    {
        otherInfo ??= MsGetOtherData.GetData(rawConfig);
        return new MsConfigLoader(new LoggerFactory().CreateLogger<MsConfigLoader>()).LoadConfig(Path.GetFullPath("/home/username/database"), rawConfig, otherInfo);
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

    [Fact]
    public void Version_Parsed_From_Config()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.Version = "16.0.1000";
        var config = LoadConfig(rawConfig);

        Assert.Equal(new Version(16, 0, 1000), config.Version);
    }

    [Fact]
    public void Version_Defaults_When_Not_Set()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.Version = null;
        var config = LoadConfig(rawConfig);

        Assert.Equal(new Version(16, 0, 0), config.Version);
    }

    [Fact]
    public void Throws_On_Invalid_Version()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        rawConfig.Version = "not-a-version";

        Assert.Throws<ConfigurationException.InvalidFieldTypeException>(() => LoadConfig(rawConfig));
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

    [Fact]
    public void DatabaseAvailable_Defaults_False_When_Not_In_OtherInfo()
    {
        var rawConfig = TestDataRawConfig.MsTestRawConfig;
        var otherInfo = MsGetOtherData.GetData(MsGetOtherData.DefaultCredentials);
        otherInfo.Remove("DatabaseAvailable");
        var config = LoadConfig(rawConfig, otherInfo);

        Assert.False(config.DatabaseAvailable);
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
        var credentials = new MsDatabaseCredentials { Host = "sqlserver", Port = 1433, Encrypt = true, TrustServerCertificate = false, ConnectionTimeout = 30 };
        Assert.Equal("sqlserver", credentials.DataSource);
    }

    [Fact]
    public void DataSource_NonDefaultPort_Includes_Port()
    {
        var credentials = new MsDatabaseCredentials { Host = "sqlserver", Port = 1434, Encrypt = true, TrustServerCertificate = false, ConnectionTimeout = 30 };
        Assert.Equal("sqlserver,1434", credentials.DataSource);
    }

    [Fact]
    public void DataSource_NamedInstance_Uses_Backslash_Notation()
    {
        var credentials = new MsDatabaseCredentials { Host = "sqlserver", Instance = "SQLEXPRESS", Encrypt = true, TrustServerCertificate = false, ConnectionTimeout = 30 };
        Assert.Equal(@"sqlserver\SQLEXPRESS", credentials.DataSource);
    }
}
