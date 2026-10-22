using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Resources;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.MySql.Configuration;
using Xunit.Sdk;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.MySql.Tests.Configuration.MyConfigLoaderTests.NumericScaleSerializer), typeof(NumericLength))]

namespace TcfOss.DatabaseManager.MySql.Tests.Configuration;

public class MyConfigLoaderTests
{
    [Fact]
    public void MyConfigurationBuilder_SuppliesMySqlDefaultsWithoutHosting()
    {
        var rawConfig = new MyConfigurationBuilder().GetDefaultConfig(Path.GetTempPath());

        Assert.Equal(SqlDialect.MySql, rawConfig.Dialect);
        Assert.Equal(QuoteStyle.Backticks, rawConfig.QuoteStyle);
        Assert.Equal("def", rawConfig.Catalog);
        Assert.Equal("3306", rawConfig.Credentials!.Port);
        Assert.Equal("INFORMATION_SCHEMA", Assert.Single(rawConfig.Schemas).SchemaName);
    }

    [Theory]
    [InlineData(null, "8.4.6")]
    [InlineData("", "8.4.6")]
    [InlineData(" ", "8.4.6")]
    [InlineData("invalid", "8.4.6")]
    [InlineData("8.0.37", "8.0.37")]
    [InlineData("8.4.1", "8.4.1")]
    public void ConfigurationBuilder_ConnectionFailure_ResolvesFallbackVersion(string? configuredVersion, string expectedVersion)
    {
        string rootDirectory = Path.GetFullPath(Path.GetTempPath());
        var builder = new MyConfigurationBuilder();
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
        Assert.Equal(SqlDialect.MySql, config.Dialect);
    }

    [Fact]
    public void Basic_Config_Loads()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        var config = LoadConfig(rawConfig);

        // Validate the loaded config
        Assert.NotNull(config);
        Assert.Equal("mycatalog", config.Catalog.Name);
        Assert.Equal(QuoteStyle.Backticks, config.QuoteStyle);
        var expectedRootPath = Path.GetFullPath("/home/username/database");
        Assert.Equal(expectedRootPath, config.ProjectDirectory);
        Assert.Equal(2, config.Schemas.Count);

        // Validate schema mappings
        var schemaValues = config.Schemas.Values.ToList();
        var schema1 = schemaValues[0];
        var schema2 = schemaValues[1];
        Assert.Equal("schema1", schema1.SchemaName.Name);
        Assert.Contains("excluded_table", schema1.ExcludeDatabaseObjectNames);

        Assert.Equal("localhost", config.DatabaseCredentials.Host);
        Assert.Equal<uint>(3306, config.DatabaseCredentials.Port);
        Assert.Equal("testuser", config.DatabaseCredentials.Username);
        Assert.Equal("testpassword", config.DatabaseCredentials.Password);

        // Validate schema paths are absolute
        Assert.True(Path.IsPathRooted(config.ProjectDirectory));
        Assert.True(Path.IsPathRooted(schema1.RootPath));
        Assert.True(Path.IsPathRooted(schema2.RootPath));
        var expectedPath1 = Path.Combine(expectedRootPath, "schema_1");
        Assert.StartsWith(expectedPath1, schema1.RootPath);
        var expectedPath2 = Path.Combine(expectedRootPath, "subdir", "schema_2");
        Assert.StartsWith(expectedPath2, schema2.RootPath);
    }

    [Fact]
    public void FillsDefaults()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        var config = LoadConfig(rawConfig);

        // Validate defaults
        Assert.Equal(QuoteStyle.Backticks, config.QuoteStyle);
        var expectedRootPath = Path.GetFullPath("/home/username/database");
        Assert.Equal(expectedRootPath, config.ProjectDirectory);
        var schemaValues = config.Schemas.Values.ToList();
        var expectedPath1 = Path.Combine(expectedRootPath, "schema_1");
        Assert.StartsWith(expectedPath1, schemaValues[0].RootPath);
        var expectedPath2 = Path.Combine(expectedRootPath, "subdir", "schema_2");
        Assert.StartsWith(expectedPath2, schemaValues[1].RootPath);
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
    public void Absolutizes_Relative_Project_Directory()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.Generic,
            ProjectDirectory = "relative/path/to/project",
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "test_schema",
                    RootPath = "test_schema_path"
                }
            ],
            Credentials = new ConfigParsing.Credentials
            {
                Hostname = "localhost",
                Port = "3306",
                Username = "testuser",
                Password = "testpassword"
            }
        };
        var config = LoadConfig(rawConfig);

        Assert.Equal(Path.GetFullPath("/home/username/database/relative/path/to/project"), config.ProjectDirectory);
        Assert.StartsWith(Path.GetFullPath("/home/username/database/relative/path/to/project/test_schema_path"), config.Schemas.Values.First().RootPath);
    }

    [Fact]
    public void Attribute_Defaults_Overrides()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfigSillyDefaults;
        var config = LoadConfig(rawConfig);

        Assert.Equal(MySqlNumericAttribute.Unsigned, config.AttributeDefaults.NumericAttribute);
        Assert.Equal<uint?>(25, config.AttributeDefaults.UnsignedBigIntWidth);
        Assert.Equal<uint?>(3, config.AttributeDefaults.SignedIntWidth);
        Assert.Equal(IndexMethod.Hash, config.AttributeDefaults.IndexMethod);
    }

    [Fact]
    public void Attribute_Defaults_Non_Overrides_MySql()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfigSillyDefaults;
        var config = LoadConfig(rawConfig);

        Assert.Equal<uint?>(3, config.AttributeDefaults.SignedIntWidth);
        Assert.Null(config.AttributeDefaults.UnsignedIntWidth);
        Assert.Null(config.AttributeDefaults.SignedBigIntWidth);
        Assert.Equal<uint?>(25, config.AttributeDefaults.UnsignedBigIntWidth);
        Assert.Null(config.AttributeDefaults.SignedMediumIntWidth);
        Assert.Null(config.AttributeDefaults.UnsignedMediumIntWidth);
        Assert.Null(config.AttributeDefaults.SignedSmallIntWidth);
        Assert.Null(config.AttributeDefaults.UnsignedSmallIntWidth);
        Assert.Null(config.AttributeDefaults.SignedTinyIntWidth);
        Assert.Null(config.AttributeDefaults.UnsignedTinyIntWidth);
        Assert.Equal(new NumericLength.PrecisionScale(10, 0), config.AttributeDefaults.DecimalPrecision);
        Assert.Null(config.AttributeDefaults.FloatPrecision);
        Assert.Null(config.AttributeDefaults.DoublePrecision);
        Assert.Null(config.AttributeDefaults.YearPrecision);
    }


    [Fact]
    public void Throws_On_Empty_SchemaRootPath()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.Generic,
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "test_schema",
                    RootPath = ""
                }
            ],
            Credentials = new ConfigParsing.Credentials
            {
                Hostname = "localhost",
                Port = "3306",
                Username = "testuser",
                Password = "testpassword"
            }
        };
        Assert.Throws<ConfigurationException.MissingFieldException>(() => LoadConfig(rawConfig));
    }

    [Fact]
    public void Throws_On_Empty_Schema_Name()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.Generic,
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "",
                    RootPath = "some/path"
                }
            ],
            Credentials = new ConfigParsing.Credentials
            {
                Hostname = "localhost",
                Port = "3306",
                Username = "testuser",
                Password = "testpassword"
            }
        };
        Assert.Throws<ConfigurationException.MissingFieldException>(() => LoadConfig(rawConfig));
    }

    [Fact]
    public void Throws_On_Missing_Credentials()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.Generic,
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "test_schema",
                    RootPath = "some/path"
                }
            ]
        };
        Assert.Throws<KeyNotFoundException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ExtendedQuoteStyle.Backticks)]
    [InlineData(ExtendedQuoteStyle.Ansi)]
    [InlineData(ExtendedQuoteStyle.SingleQuote)]
    [InlineData(ExtendedQuoteStyle.Brackets)]
    [InlineData(ExtendedQuoteStyle.None)]
    public void DefaultDefiner_ParsesExtendedQuotes(ExtendedQuoteStyle quoteStyle)
    {
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig();
        var definerAccount = "my_user";
        var definerHost = "localhost";
        rawConfig.DefaultDefinerAccount = quoteStyle switch
        {
            ExtendedQuoteStyle.Backticks => $"`{definerAccount}`",
            ExtendedQuoteStyle.Ansi => $"\"{definerAccount}\"",
            ExtendedQuoteStyle.SingleQuote => $"'{definerAccount}'",
            ExtendedQuoteStyle.Brackets => $"[{definerAccount}]",
            _ => definerAccount
        };
        rawConfig.DefaultDefinerHost = quoteStyle switch
        {
            ExtendedQuoteStyle.Backticks => $"`{definerHost}`",
            ExtendedQuoteStyle.Ansi => $"\"{definerHost}\"",
            ExtendedQuoteStyle.SingleQuote => $"'{definerHost}'",
            ExtendedQuoteStyle.Brackets => $"[{definerHost}]",
            _ => definerHost
        };

        var config = LoadConfig(rawConfig);

        Assert.NotNull(config.DefaultDefiner);
        var asIdentity = Assert.IsType<Account.IdentityWithHost>(config.DefaultDefiner.Account, exactMatch: true);
        Assert.Equal(definerAccount, asIdentity.Name.Name);
        Assert.Equal(quoteStyle, asIdentity.Name.QuoteStyle);
        Assert.Equal(definerHost, asIdentity.Host.Name);
        Assert.Equal(quoteStyle, asIdentity.Host.QuoteStyle);
    }

    [Fact]
    public void Throws_On_DefaultDefinerHost_Without_DefaultDefinerAccount()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.DefaultDefinerAccount = null;
        rawConfig.DefaultDefinerHost = "`localhost`";

        Assert.Throws<ConfigurationException.DefinerHostWithoutAccount>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData("CURRENT_USER")]
    [InlineData("CURRENT_ROLE")]
    [InlineData("SESSION_USER something")]
    public void Throws_On_DefaultDefinerAccount_ImplicitIdentity(string defaultDefinerAccount)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.DefaultDefinerAccount = defaultDefinerAccount;
        rawConfig.DefaultDefinerHost = null;

        Assert.Throws<ConfigurationException.ImplicitDefiner>(() => LoadConfig(rawConfig));
    }

    [Fact]
    public void Throws_On_DefaultDefinerHost_ImplicitIdentity()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.DefaultDefinerAccount = "`my_user`";
        rawConfig.DefaultDefinerHost = "CURRENT_USER";

        Assert.Throws<ConfigurationException.ImplicitDefiner>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [ClassData(typeof(NumericLengthTestData))]
    public void AttributeDefaults_DoublePrecision(int? doublePrecision, int? doubleScale, NumericLength? expectedNumericLength, ExceptionType? thrownExceptionType, string? appliesTo)
    {
        if (appliesTo != null && appliesTo != "Double")
        {
            return;
        }

        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            DoublePrecision = doublePrecision,
            DoubleScale = doubleScale
        };

        if (thrownExceptionType != null)
        {
            ConfigurationException exception;
            if (thrownExceptionType == ExceptionType.ScaleWithoutPrecision)
            {
                exception = Assert.Throws<ConfigurationException.InvalidNumericScaleMissingPrecision>(() => LoadConfig(rawConfig));
            }
            else
            {
                exception = Assert.Throws<ConfigurationException.InvalidNumericScaleGreaterThanPrecision>(() => LoadConfig(rawConfig));
            }
            Assert.Equal(NumericLengthTestData.GetFormattedMessage(thrownExceptionType.Value, "Double", doublePrecision, doubleScale), exception.Message);
            return;
        }

        var config = LoadConfig(rawConfig);
        Assert.Equal(expectedNumericLength, config.AttributeDefaults.DoublePrecision);
    }

    [Theory]
    [ClassData(typeof(NumericLengthTestData))]
    public void AttributeDefaults_Float(int? floatPrecision, int? floatScale, NumericLength? expectedNumericLength, ExceptionType? thrownExceptionType, string? appliesTo)
    {
        if (appliesTo != null && appliesTo != "Float")
        {
            return;
        }

        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            FloatPrecision = floatPrecision,
            FloatScale = floatScale
        };

        if (thrownExceptionType != null)
        {
            ConfigurationException exception;
            if (thrownExceptionType == ExceptionType.ScaleWithoutPrecision)
            {
                exception = Assert.Throws<ConfigurationException.InvalidNumericScaleMissingPrecision>(() => LoadConfig(rawConfig));
            }
            else
            {
                exception = Assert.Throws<ConfigurationException.InvalidNumericScaleGreaterThanPrecision>(() => LoadConfig(rawConfig));
            }
            Assert.Equal(NumericLengthTestData.GetFormattedMessage(thrownExceptionType.Value, "Float", floatPrecision, floatScale), exception.Message);
            return;
        }

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedNumericLength, config.AttributeDefaults.FloatPrecision);
    }

    [Fact]
    public void Missing_Credentials_Throws()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.Credentials = null;

        var ex = Assert.Throws<ConfigurationException.MissingFieldException>(() => BuildConfig(rawConfig));
        Assert.Contains("Credentials", ex.Message);
    }

    [Fact]
    public void Missing_Credentials_Port_Supplies_Default()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.Credentials!.Port = null;

        var config = BuildConfig(rawConfig);
        Assert.Equal(3306u, config.DatabaseCredentials.Port);
    }

    [Fact]
    public void Missing_Username_Throws()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.Credentials!.Username = null;

        var ex = Assert.Throws<ConfigurationException.MissingFieldException>(() => BuildConfig(rawConfig));
        Assert.Contains("Credentials.Username", ex.Message);
    }

    [Theory]
    [ClassData(typeof(NumericLengthTestData))]
    public void AttributeDefaults_Decimal(int? decimalPrecision, int? decimalScale, NumericLength? expectedNumericLength, ExceptionType? thrownExceptionType, string? appliesTo)
    {
        if (appliesTo != null && appliesTo != "Decimal")
        {
            return;
        }

        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            DecimalPrecision = decimalPrecision,
            DecimalScale = decimalScale
        };

        if (thrownExceptionType != null)
        {
            ConfigurationException exception;
            if (thrownExceptionType == ExceptionType.ScaleWithoutPrecision)
            {
                exception = Assert.Throws<ConfigurationException.InvalidNumericScaleMissingPrecision>(() => LoadConfig(rawConfig));
            }
            else
            {
                exception = Assert.Throws<ConfigurationException.InvalidNumericScaleGreaterThanPrecision>(() => LoadConfig(rawConfig));
            }
            Assert.Equal(NumericLengthTestData.GetFormattedMessage(thrownExceptionType.Value, "Decimal", decimalPrecision, decimalScale), exception.Message);
            return;
        }

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedNumericLength, config.AttributeDefaults.DecimalPrecision);
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.MySqlNumericAttribute.Signed, "SIGNED")]
    [InlineData(ConfigParsing.Attributes.MySqlNumericAttribute.Unsigned, "UNSIGNED")]
    [InlineData(ConfigParsing.Attributes.MySqlNumericAttribute.Zerofill, "ZEROFILL")]
    [InlineData(ConfigParsing.Attributes.MySqlNumericAttribute.NotSet, "SIGNED")]
    [InlineData(null, "SIGNED")]
    [InlineData((ConfigParsing.Attributes.MySqlNumericAttribute)999, "SIGNED")]
    public void AttributeDefaults_NumericAttribute(ConfigParsing.Attributes.MySqlNumericAttribute numericAttribute, string? expectedNumericAttribute)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            NumericAttribute = numericAttribute
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedNumericAttribute, config.AttributeDefaults.NumericAttribute?.ToString());
    }

    [Fact]
    public void AttributeDefaults_Indexes_Constraints_Default()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults();

        var config = LoadConfig(rawConfig);

        Assert.Equal(IndexMethod.Btree, config.AttributeDefaults.IndexMethod);
        Assert.Equal(ReferentialAction.NoAction, config.AttributeDefaults.ForeignKeyOnUpdate);
        Assert.Equal(ReferentialAction.NoAction, config.AttributeDefaults.ForeignKeyOnDelete);
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.IndexMethod.NotSet, "BTREE")]
    [InlineData(ConfigParsing.Attributes.IndexMethod.Btree, "BTREE")]
    [InlineData(ConfigParsing.Attributes.IndexMethod.Hash, "HASH")]
    [InlineData(ConfigParsing.Attributes.IndexMethod.Rtree, "RTREE")]
    public void AttributeDefaults_IndexMethod(ConfigParsing.Attributes.IndexMethod indexMethod, string expectedIndexMethod)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            IndexMethod = indexMethod
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedIndexMethod, config.AttributeDefaults.IndexMethod.ToString());
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.IndexMethod.Gin)]
    [InlineData(ConfigParsing.Attributes.IndexMethod.Gist)]
    [InlineData(ConfigParsing.Attributes.IndexMethod.SpGist)]
    [InlineData(ConfigParsing.Attributes.IndexMethod.Brin)]
    [InlineData((ConfigParsing.Attributes.IndexMethod)999)]
    public void AttributeDefaults_IndexMethod_Invalid(ConfigParsing.Attributes.IndexMethod indexMethod)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            IndexMethod = indexMethod
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.NoAction, "NO ACTION")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.Restrict, "RESTRICT")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.Cascade, "CASCADE")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.SetNull, "SET NULL")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.SetDefault, "SET DEFAULT")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.NotSet, "NO ACTION")]
    public void AttributeDefaults_ForeignKey_OnUpdate(ConfigParsing.Attributes.ReferentialAction referentialAction, string expectedReferentialAction)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ForeignKeyOnUpdate = referentialAction
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedReferentialAction, config.AttributeDefaults.ForeignKeyOnUpdate?.ToString());
    }

    [Theory]
    [InlineData((ConfigParsing.Attributes.ReferentialAction)999)]
    public void AttributeDefaults_ForeignKey_OnUpdate_OtherThrows(ConfigParsing.Attributes.ReferentialAction referentialAction)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ForeignKeyOnUpdate = referentialAction
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.NoAction, "NO ACTION")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.Restrict, "RESTRICT")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.Cascade, "CASCADE")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.SetNull, "SET NULL")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.SetDefault, "SET DEFAULT")]
    [InlineData(ConfigParsing.Attributes.ReferentialAction.NotSet, "NO ACTION")]
    public void AttributeDefaults_ForeignKey_OnDelete(ConfigParsing.Attributes.ReferentialAction referentialAction, string expectedReferentialAction)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ForeignKeyOnDelete = referentialAction
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedReferentialAction, config.AttributeDefaults.ForeignKeyOnDelete?.ToString());
    }

    [Theory]
    [InlineData((ConfigParsing.Attributes.ReferentialAction)999)]
    public void AttributeDefaults_ForeignKey_OnDelete_OtherThrows(ConfigParsing.Attributes.ReferentialAction referentialAction)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ForeignKeyOnDelete = referentialAction
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.ContainsSql, "CONTAINS SQL")]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.ReadsSqlData, "READS SQL DATA")]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.ModifiesSqlData, "MODIFIES SQL DATA")]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.NoSql, "NO SQL")]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.NotSet, "CONTAINS SQL")]
    [InlineData(null, null)]
    public void AttributeDefaults_FunctionDataRelation(ConfigParsing.Attributes.SqlDataRelation? sqlDataRelation, string? expectedDataRelation)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            FunctionDataRelation = sqlDataRelation
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedDataRelation, config.AttributeDefaults.FunctionDataRelation?.ToString());
    }

    [Theory]
    [InlineData((ConfigParsing.Attributes.SqlDataRelation)999)]
    public void AttributeDefaults_FunctionDataRelation_OtherThrows(ConfigParsing.Attributes.SqlDataRelation? sqlDataRelation)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            FunctionDataRelation = sqlDataRelation
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.ContainsSql, "CONTAINS SQL")]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.ReadsSqlData, "READS SQL DATA")]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.ModifiesSqlData, "MODIFIES SQL DATA")]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.NoSql, "NO SQL")]
    [InlineData(ConfigParsing.Attributes.SqlDataRelation.NotSet, "CONTAINS SQL")]
    [InlineData(null, null)]
    public void AttributeDefaults_ProcedureDataRelation(ConfigParsing.Attributes.SqlDataRelation? sqlDataRelation, string? expectedDataRelation)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ProcedureDataRelation = sqlDataRelation
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedDataRelation, config.AttributeDefaults.ProcedureDataRelation?.ToString());
    }

    [Theory]
    [InlineData((ConfigParsing.Attributes.SqlDataRelation)999)]
    public void AttributeDefaults_ProcedureDataRelation_OtherThrows(ConfigParsing.Attributes.SqlDataRelation? sqlDataRelation)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ProcedureDataRelation = sqlDataRelation
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.SecurityContext.Definer, "DEFINER")]
    [InlineData(ConfigParsing.Attributes.SecurityContext.Invoker, "INVOKER")]
    [InlineData(ConfigParsing.Attributes.SecurityContext.NotSet, "DEFINER")]
    public void AttributeDefaults_FunctionSecurityContext(ConfigParsing.Attributes.SecurityContext? securityContext, string? expectedSecurityContext)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            FunctionSecurityContext = securityContext
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedSecurityContext, config.AttributeDefaults.FunctionSecurityContext.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData((ConfigParsing.Attributes.SecurityContext)999)]
    public void AttributeDefaults_FunctionSecurityContext_OtherThrows(ConfigParsing.Attributes.SecurityContext? securityContext)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            FunctionSecurityContext = securityContext
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.SecurityContext.Definer, "DEFINER")]
    [InlineData(ConfigParsing.Attributes.SecurityContext.Invoker, "INVOKER")]
    [InlineData(ConfigParsing.Attributes.SecurityContext.NotSet, "DEFINER")]
    public void AttributeDefaults_ProcedureSecurityContext(ConfigParsing.Attributes.SecurityContext? securityContext, string? expectedSecurityContext)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ProcedureSecurityContext = securityContext
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedSecurityContext, config.AttributeDefaults.ProcedureSecurityContext.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData((ConfigParsing.Attributes.SecurityContext)999)]
    public void AttributeDefaults_ProcedureSecurityContext_OtherThrows(ConfigParsing.Attributes.SecurityContext? securityContext)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ProcedureSecurityContext = securityContext
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.SecurityContext.Definer, "DEFINER")]
    [InlineData(ConfigParsing.Attributes.SecurityContext.Invoker, "INVOKER")]
    [InlineData(ConfigParsing.Attributes.SecurityContext.NotSet, "DEFINER")]
    public void AttributeDefaults_ViewSecurityContext(ConfigParsing.Attributes.SecurityContext? securityContext, string? expectedSecurityContext)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ViewSecurityContext = securityContext
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedSecurityContext, config.AttributeDefaults.ViewSecurityContext.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData((ConfigParsing.Attributes.SecurityContext)999)]
    public void AttributeDefaults_ViewSecurityContext_OtherThrows(ConfigParsing.Attributes.SecurityContext? securityContext)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ViewSecurityContext = securityContext
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.RoutineParameterDirection.In, "IN")]
    [InlineData(ConfigParsing.Attributes.RoutineParameterDirection.Out, "OUT")]
    [InlineData(ConfigParsing.Attributes.RoutineParameterDirection.InOut, "INOUT")]
    [InlineData(ConfigParsing.Attributes.RoutineParameterDirection.NotSet, null)]
    [InlineData(null, null)]
    public void AttributeDefaults_FunctionParameterDirection(ConfigParsing.Attributes.RoutineParameterDirection? parameterDirection, string? expectedParameterDirection)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            FunctionParameterDirection = parameterDirection
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedParameterDirection, config.AttributeDefaults.FunctionParameterDirection?.ToString());
    }

    [Fact]
    public void AttributeDefaults_FunctionParameterDirection_OtherThrows()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            FunctionParameterDirection = (ConfigParsing.Attributes.RoutineParameterDirection)999
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }

    [Theory]
    [InlineData(ConfigParsing.Attributes.RoutineParameterDirection.In, "IN")]
    [InlineData(ConfigParsing.Attributes.RoutineParameterDirection.Out, "OUT")]
    [InlineData(ConfigParsing.Attributes.RoutineParameterDirection.InOut, "INOUT")]
    [InlineData(ConfigParsing.Attributes.RoutineParameterDirection.NotSet, "IN")]
    [InlineData(null, null)]
    public void AttributeDefaults_ProcedureParameterDirection(ConfigParsing.Attributes.RoutineParameterDirection? parameterDirection, string? expectedParameterDirection)
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ProcedureParameterDirection = parameterDirection
        };

        var config = LoadConfig(rawConfig);

        Assert.Equal(expectedParameterDirection, config.AttributeDefaults.ProcedureParameterDirection?.ToString());
    }

    [Fact]
    public void AttributeDefaults_ProcedureParameterDirection_OtherThrows()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            ProcedureParameterDirection = (ConfigParsing.Attributes.RoutineParameterDirection)999
        };

        Assert.Throws<ConfigurationException.InvalidFieldOneOfException>(() => LoadConfig(rawConfig));
    }


    [Fact]
    public void NormalizationSettings_Defaults()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        var config = LoadConfig(rawConfig);

        Assert.Equal(IdentifierQuotationHandling.Always, config.NormalizationSettings.IdentifierQuotationHandling);
        Assert.Equal(IdentifierQuotationHandling.Always, config.NormalizationSettings.CteDeclarationNameQuotationHandling);
        Assert.Equal(ExtendedQuoteStyle.Backticks, config.NormalizationSettings.AccountQuoteStyle);
        Assert.True(config.NormalizationSettings.CastConvertVarcharToChar);
        Assert.True(config.NormalizationSettings.CastAddCharsetToType);
    }

    [Fact]
    public void NormalizationSettings_Overrides()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;

        var updatedNormalizationSettings = new ConfigParsing.NormalizationSettings
        {
            IdentifierQuotationHandling = IdentifierQuotationHandling.IfSpecial,
            CteDeclarationNameQuotationHandling = IdentifierQuotationHandling.OnlyIfSpecial,
            AccountQuoteStyle = ExtendedQuoteStyle.SingleQuote
        };

        rawConfig.ViewNormalizationSettings = updatedNormalizationSettings;

        var config = LoadConfig(rawConfig);

        Assert.Equal(IdentifierQuotationHandling.IfSpecial, config.NormalizationSettings.IdentifierQuotationHandling);
        Assert.Equal(IdentifierQuotationHandling.OnlyIfSpecial, config.NormalizationSettings.CteDeclarationNameQuotationHandling);
        Assert.Equal(ExtendedQuoteStyle.SingleQuote, config.NormalizationSettings.AccountQuoteStyle);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseSettings_RemoveSlashes_Overrides(bool removeSlashes)
    {
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig();
        rawConfig.ParseSettings = new Core.Configuration.ParseSettings { RemoveSlashesBeforeQuotesGenerationExpression = removeSlashes };

        var config = LoadConfig(rawConfig);

        Assert.Equal(removeSlashes, config.ParseSettings.RemoveSlashesBeforeQuotesGenerationExpression);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("1.0.0", false)]
    [InlineData("1.0.0", true)]
    [InlineData("invalid", false)]
    [InlineData("invalid", true)]
    public void Version_UsesResolvedOtherInformation(string? configuredVersion, bool databaseAvailable)
    {
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig();
        rawConfig.Version = configuredVersion;
        var version = new Version(8, 0, 37);
        var otherInfo = MyGetOtherData.GetData(rawConfig, version, databaseAvailable);

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
        var otherInfo = MyGetOtherData.GetData(rawConfig);
        otherInfo.Remove(key);

        Assert.Throws<KeyNotFoundException>(() => LoadConfig(rawConfig, otherInfo));
    }

    [Fact]
    public void LoadCredentials_FromEnvironmentVariables()
    {
        var credentials = new ConfigParsing.Credentials
        {
            Hostname = "${ENV:DB_HOST}",
            Port = "${ENV:DB_PORT}",
            Username = "${ENV:DB_USER}",
            Password = "${ENV:DB_PASSWORD}",
            SocketPath = "${ENV:DB_SOCKET}",
            DefaultCharset = "${ENV:DB_CHARSET}",
            ConnectionTimeout = 1,
        };
        var variables = new Dictionary<string, string>
        {
            { "DB_HOST", "127.0.0.1" },
            { "DB_PORT", "1" },
            { "DB_USER", "envuser" },
            { "DB_PASSWORD", "password${ENV:NOT_ANOTHER_VARIABLE}" },
            { "DB_SOCKET", Path.Combine(Path.GetTempPath(), "missing-mysql-env-test.sock") },
            { "DB_CHARSET", "utf8mb4" },
        };

        MyConfig config = BuildConfig(credentials, variables);

        Assert.Equal(variables["DB_HOST"], config.DatabaseCredentials.Host);
        Assert.Equal(1u, config.DatabaseCredentials.Port);
        Assert.Equal(variables["DB_USER"], config.DatabaseCredentials.Username);
        Assert.Equal(variables["DB_PASSWORD"], config.DatabaseCredentials.Password);
        Assert.Equal(variables["DB_SOCKET"], config.DatabaseCredentials.SocketPath);
        Assert.Equal(variables["DB_CHARSET"], config.DatabaseCredentials.DefaultCharset);
    }

    [Theory]
    [InlineData("notanint")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    public void LoadCredentials_InvalidPortFromEnvironment_Throws(string port)
    {
        var credentials = new ConfigParsing.Credentials
        {
            Hostname = "127.0.0.1",
            Port = "${ENV:DB_PORT}",
            Username = "testuser",
            Password = "testpassword",
            ConnectionTimeout = 1,
        };

        var exception = Assert.Throws<ConfigurationException.InvalidPortException>(() =>
            BuildConfig(credentials, new Dictionary<string, string> { { "DB_PORT", port } }));

        Assert.Equal($"Configuration Error: The port value '{port}' is invalid. It must be an unsigned integer between 1 and 65535.", exception.Message);
    }


    // === Configuration Helpers ===

    private static MyConfig LoadConfig(ConfigParsing.Config rawConfig, Dictionary<string, object>? otherInfo = null)
    {
        otherInfo ??= MyGetOtherData.GetData(rawConfig);
        return new MyConfigLoader(new LoggerFactory().CreateLogger<MyConfigLoader>()).LoadConfig(Path.GetFullPath("/home/username/database"), rawConfig, otherInfo);
    }

    private static MyConfig BuildConfig(ConfigParsing.Config rawConfig)
    {
        var testLogger = NullLogger.Instance;
        var rootPath = rawConfig.ProjectDirectory ?? "/default/path/";
        var builder = new MyConfigurationBuilder();
        return builder.BuildConfiguration(rootPath, rawConfig, new EnvironmentVariableReader(), false, testLogger);
    }

    private static MyConfig BuildConfig(ConfigParsing.Credentials credentials, Dictionary<string, string> variables)
    {
        string rootDirectory = Path.GetFullPath(Path.GetTempPath());
        var builder = new MyConfigurationBuilder();
        ConfigParsing.Config rawConfig = builder.GetDefaultConfig(rootDirectory);
        rawConfig.Credentials = credentials;
        return builder.BuildConfiguration(rootDirectory, rawConfig, new TestEnvironmentReader(variables), false, NullLogger.Instance);
    }


    // === Test Data Fixtures ===

    class NumericLengthTestData : IEnumerable<TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>>
    {
        public IEnumerator<TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>> GetEnumerator()
        {
            yield return new TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>(null, null, null, null, null);
            yield return new TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>(10, null, new NumericLength.Precision(10), null, null);
            yield return new TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>(10, 2, new NumericLength.PrecisionScale(10, 2), null, null);
            yield return new TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>(0, 0, new NumericLength.PrecisionScale(0, 0), null, null);
            yield return new TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>(5, 10, null, ExceptionType.ScaleGreaterThanPrecision, null);
            yield return new TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>(null, 2, null, ExceptionType.ScaleWithoutPrecision, null);
            yield return new TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>(-1, -1, new NumericLength.PrecisionScale(10, 0), null, "Decimal");
            yield return new TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>(-1, -1, null, null, "Float");
            yield return new TheoryDataRow<int?, int?, NumericLength?, ExceptionType?, string?>(-1, -1, null, null, "Double");
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public static string GetFormattedMessage(ExceptionType exceptionType, string label, int? precision, int? scale)
        {
            string body = exceptionType == ExceptionType.ScaleGreaterThanPrecision
                ? string.Format(ErrorMessages.Err_Conf_Numeric_ScaleGreaterThanPrecision, label, scale, precision)
                : string.Format(ErrorMessages.Err_Conf_Numeric_ScaleWithoutPrecision, label);
            return string.Format(ErrorMessages.ErrWithType, ErrorMessages.Err_Conf, body);
        }
    }

    public enum ExceptionType
    {
        ScaleGreaterThanPrecision,
        ScaleWithoutPrecision
    }

    public class NumericScaleSerializer : IXunitSerializer
    {
        public string Serialize(object? value)
        {
            if (value is NumericLength.PrecisionScale ps)
            {
                return $"({ps.Length}, {ps.Scale})";
            }
            else if (value is NumericLength.Precision p)
            {
                return $"({p.Length})";
            }
            else if (value == null)
            {
                return "null";
            }
            throw new InvalidOperationException($"Cannot serialize value of type {value.GetType().FullName} in NumericScaleSerializer");
        }

        public object Deserialize(Type type, string serializedValue)
        {
            if (type == typeof(NumericLength))
            {
                if (serializedValue == "null")
                {
                    return null!;
                }
                else if (serializedValue.StartsWith('(') && serializedValue.EndsWith(')'))
                {
                    var content = serializedValue[1..^1];
                    var parts = content.Split(',').Select(p => p.Trim()).ToArray();
                    if (parts.Length == 1 && int.TryParse(parts[0], out var length))
                    {
                        return new NumericLength.Precision((uint)length);
                    }
                    else if (parts.Length == 2 && int.TryParse(parts[0], out var len) && int.TryParse(parts[1], out int scale))
                    {
                        return new NumericLength.PrecisionScale((uint)len, (uint)scale);
                    }
                }
                throw new ArgumentException($"Cannot deserialize value '{serializedValue}' to NumericLength");
            }
            throw new ArgumentException($"Cannot deserialize to type {type.FullName}");
        }

        public bool IsSerializable(Type type, object? value, out string failureReason)
        {
            if (type == typeof(NumericLength) && (value is NumericLength || value == null))
            {
                failureReason = string.Empty;
                return true;
            }
            failureReason = $"Type {type.FullName} is not supported by NumericScaleSerializer";
            return false;
        }
    }
}
