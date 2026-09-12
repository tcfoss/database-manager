using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.Tests.Configuration;

public class EnvironmentVariablesTest
{
    [Fact]
    public void LoadCredentials_PasswordFromEnv()
    {
        var givenCredentials = new Credentials
        {
            Hostname = "localhost",
            Port = "3306",
            Username = "testuser",
            Password = "${ENV:DB_PASSWORD_ENV}"
        };

        var config = BuildConfig(givenCredentials, new Dictionary<string, string>
        {
            { "DB_PASSWORD_ENV", "envpasswordvalue" }
        });

        Assert.Equal("envpasswordvalue", config.DatabaseCredentials.Password);
    }

    [Fact]
    public void LoadCredentials_PortFromEnv()
    {
        var givenCredentials = new Credentials
        {
            Hostname = "localhost",
            Port = "${ENV:DB_PORT_ENV}",
            Username = "testuser",
            Password = "testpassword"
        };

        var config = BuildConfig(givenCredentials, new Dictionary<string, string>
        {
            { "DB_PORT_ENV", "3307" }
        });

        Assert.Equal(3307u, config.DatabaseCredentials.Port);
    }

    [Fact]
    public void LoadCredentials_SocketPathFromEnv()
    {
        var givenCredentials = new Credentials
        {
            Hostname = "localhost",
            Port = "3306",
            Username = "testuser",
            Password = "testpassword",
            SocketPath = "${ENV:DB_SOCKET_ENV}"
        };

        var config = BuildConfig(givenCredentials, new Dictionary<string, string>
        {
            { "DB_SOCKET_ENV", "/var/strangeplace/mysqld/mysqld.sock" }
        });

        Assert.Equal("/var/strangeplace/mysqld/mysqld.sock", config.DatabaseCredentials.SocketPath);
    }

    [Fact]
    public void LoadCredentials_HostFromEnv()
    {
        var givenCredentials = new Credentials
        {
            Hostname = "${ENV:DB_HOST_ENV}",
            Port = "3306",
            Username = "testuser",
            Password = "testpassword"
        };

        var config = BuildConfig(givenCredentials, new Dictionary<string, string>
        {
            { "DB_HOST_ENV", "dbserver.example.com" }
        });

        Assert.Equal("dbserver.example.com", config.DatabaseCredentials.Host);
    }

    [Fact]
    public void LoadCredentials_UsernameFromEnv()
    {
        var givenCredentials = new Credentials
        {
            Hostname = "localhost",
            Port = "3306",
            Username = "${ENV:DB_USER_ENV}",
            Password = "testpassword"
        };

        var config = BuildConfig(givenCredentials, new Dictionary<string, string>
        {
            { "DB_USER_ENV", "envuser" }
        });

        Assert.Equal("envuser", config.DatabaseCredentials.Username);
    }

    [Fact]
    public void LoadCredentials_NonIntPortFromEnv_Throws()
    {
        var givenCredentials = new Credentials
        {
            Hostname = "localhost",
            Port = "${ENV:DB_PORT_ENV}",
            Username = "testuser",
            Password = "testpassword"
        };

        var exception = Assert.Throws<ConfigurationException.InvalidPortException>(() => BuildConfig(givenCredentials, new Dictionary<string, string>
        {
            { "DB_PORT_ENV", "notanint" }
        }));

        var expected = "Configuration Error: The port value 'notanint' is invalid. It must be an unsigned integer between 1 and 65535.";

        Assert.Equal(expected, exception.Message);
    }

    private static MyConfig BuildConfig(Credentials creds, Dictionary<string, string> envVariables)
    {
        var startup = new MyStartup();
        var rawConfig = GetTestRawConfig(creds);
        return startup.BuildConfiguration("/my/root/dir", rawConfig, CreateEnvironmentVariableReader(envVariables), relaxed: false, logger: NullLogger.Instance);
    }

    private static Config GetTestRawConfig(Credentials creds)
    {
        return new Config()
        {
            Catalog = "mycatalog",
            Dialect = SqlDialect.MySql,
            Schemas =
            [
                new SchemaMapping
                {
                    SchemaName = "schema1",
                    RootPath = "schema_1",
                    ExcludeDatabaseObjectNames = ["excluded_table"],
                    ExcludeFilePatterns = ["**/*badfile.sql"]
                },
                new SchemaMapping
                {
                    SchemaName = "schema2",
                    RootPath = "subdir/schema_2",
                    ExcludeDatabaseObjectNames = [],
                    ExcludeFilePatterns = []
                }
            ],
            Credentials = creds,
        };
    }

    private static TestEnvironmentReader CreateEnvironmentVariableReader(Dictionary<string, string> variables)
    {
        return new TestEnvironmentReader(variables);
    }

    class TestEnvironmentReader(Dictionary<string, string> variables) : EnvironmentVariableReader
    {
        readonly Dictionary<string, string> _variables = variables;

        public override string GetValue(string name)
        {
            return _variables[name];
        }
    }
}
