using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.MySql.App;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;
namespace TcfOss.DatabaseManager.MySql.IntegrationTests;

public class EnvironmentConfigTests
{
    [Fact]
    public void LoadCredentials_FromEnvironment_Correct()
    {
        // Only one test function messing with actual environment variables due
        // to parallel test execution issues.

        var givenCredentials = new ConfigParsing.Credentials
        {
            Hostname = "${ENV:DB_HOST_ENV}suffix",
            Username = "prefix${ENV:DB_USER_ENV}",
            Port = "${ENV:DB_PORT_ENV}",
            Password = "pre${ENV:something}${ENV:DB_PASSWORD_ENV}",
            SocketPath = "${ENV:DB_SOCKET_ENV}"
        };

        var rawConfig = new ConfigParsing.Config
        {
            Catalog = "mycatalog",
            Dialect = SqlDialect.MySql,
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "schema1",
                    RootPath = "schema_1",
                    ExcludeDatabaseObjectNames = ["excluded_table"],
                    ExcludeFilePatterns = ["**/*badfile.sql"]
                },
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "schema2",
                    RootPath = "subdir/schema_2",
                    ExcludeDatabaseObjectNames = [],
                    ExcludeFilePatterns = []
                }
            ],
            Credentials = givenCredentials,
        };

        var startup = new MyStartup();

        var envReader = new EnvironmentVariableReader();

        Environment.SetEnvironmentVariable("DB_HOST_ENV", "envhost");
        Environment.SetEnvironmentVariable("DB_USER_ENV", "envuser");
        Environment.SetEnvironmentVariable("DB_PASSWORD_ENV", "envpasswordvalue");
        Environment.SetEnvironmentVariable("DB_PORT_ENV", "3315");
        Environment.SetEnvironmentVariable("DB_SOCKET_ENV", "/var/lib/mysql/mysql.shoe");
        Environment.SetEnvironmentVariable("something", "hello");

        var config = startup.BuildConfiguration(
            CommonHelpers.InitialSourceDirectory.FullName,
            rawConfig,
            envReader,
            false,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<MyStartup>()
        );

        Assert.Equal("envhostsuffix", config.DatabaseCredentials.Host);
        Assert.Equal("prefixenvuser", config.DatabaseCredentials.Username);
        Assert.Equal("prehelloenvpasswordvalue", config.DatabaseCredentials.Password);
        Assert.Equal(3315u, config.DatabaseCredentials.Port);
        Assert.Equal("/var/lib/mysql/mysql.shoe", config.DatabaseCredentials.SocketPath);

        Assert.Null(envReader.SubstituteVariables(null!));
        Assert.Empty(envReader.SubstituteVariables(""));
        Assert.Null(envReader.TryGetValue("NON_EXISTENT_ENV_VAR"));
        Assert.Throws<ConfigurationException.EnvironmentVariableNotSet>(() => envReader.GetValue("NON_EXISTENT_ENV_VAR"));
    }
}
