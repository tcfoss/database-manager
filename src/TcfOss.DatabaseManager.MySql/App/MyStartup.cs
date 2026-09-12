using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;

namespace TcfOss.DatabaseManager.MySql.App;

public partial class MyStartup : Startup<MyConfig>
{
    protected virtual SqlDialect DefaultDialect => SqlDialect.MySql;
    private const QuoteStyle DefaultQuoteStyle = QuoteStyle.Backticks;

    public override Config GetDefaultConfig(string rootDirectory)
    {
        return new Config()
        {
            ProjectDirectory = rootDirectory,
            Catalog = "def",
            QuoteStyle = DefaultQuoteStyle,
            Dialect = DefaultDialect,
            Credentials = new Credentials
            {
                Username = Environment.UserName,
                Hostname = "localhost",
                Port = "3306",
                SocketPath = "/var/run/mysqld/mysqld.sock"
            },
            Schemas = [
                new SchemaMapping
                {
                    SchemaName = "INFORMATION_SCHEMA",
                    RootPath = "."
                }
            ]
        };
    }

    protected static DatabaseCredentials ExtractDatabaseCredentials(Config config, IReadEnvironmentVariables environmentVariableReader)
    {
        if (config.Credentials == null)
        {
            throw new ConfigurationException.MissingFieldException("Credentials");
        }

        string? portString = config.Credentials.Port != null ? environmentVariableReader.SubstituteVariables(config.Credentials.Port) : null;
        uint port;
        if (portString == null)
        {
            port = 3306;
        }
        else if (uint.TryParse(portString, out port) && port is > 0 and <= 65535)
        {
            // Port is set correctly
        }
        else
        {
            throw new ConfigurationException.InvalidPortException(portString);
        }

        if (config.Credentials.Username == null)
        {
            throw new ConfigurationException.MissingFieldException("Credentials.Username");
        }

        string? defaultCharset = config.Credentials.DefaultCharset != null
            ? environmentVariableReader.SubstituteVariables(config.Credentials.DefaultCharset)
            : null;

        var credentials = new DatabaseCredentials
        {
            Host = environmentVariableReader.SubstituteVariables(config.Credentials.Hostname),
            Username = environmentVariableReader.SubstituteVariables(config.Credentials.Username),
            Password = config.Credentials.Password != null ? environmentVariableReader.SubstituteVariables(config.Credentials.Password) : null,
            SocketPath = config.Credentials.SocketPath != null ? environmentVariableReader.SubstituteVariables(config.Credentials.SocketPath) : null,
            Port = port,
            DefaultCharset = defaultCharset ?? "utf8mb4",
            ConnectionTimeout = config.Credentials.ConnectionTimeout != null ? (uint)config.Credentials.ConnectionTimeout : DefaultTimeoutSeconds,
        };

        return credentials;
    }

    public override MyConfig BuildConfiguration(string rootDirectory, Config rawConfig, IReadEnvironmentVariables environmentVariableReader, bool relaxed, ILogger logger)
    {
        var loader = new MyConfigLoader(logger);
        bool canConnect = true;

        DatabaseCredentials credentials = ExtractDatabaseCredentials(rawConfig, environmentVariableReader);

        ServerVersion serverVersion;
        Version version;
        try
        {
            serverVersion = ServerVersion.AutoDetect(credentials.ConnectionString);
            version = serverVersion.Version;
        }
        catch (MySqlConnector.MySqlException ex)
        {
            canConnect = false;
            string credentialsString = rawConfig.Logging.EnableSensitiveDataLogging
                ? credentials.ConnectionString
                : credentials.GetRedactedConnectionString();
            s_logCantConnect(logger, credentialsString, ex);
            if (rawConfig.Version == null || !Version.TryParse(rawConfig.Version, out version!))
            {
                version = loader.DefaultVersion;
            }
            serverVersion = ServerVersion.Create(version, Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql);
        }

        Dictionary<string, object> otherInformation = GetOtherInformation(canConnect, credentials, SqlDialect.MySql, version, serverVersion, rawConfig.Catalog ?? "def", rawConfig.Schemas, logger);

        MyConfig config = loader.LoadConfig(
            rootDirectory,
            rawConfig,
            otherInformation,
            relaxed
        );

        return config;
    }

    protected virtual Dictionary<string, object> GetOtherInformation(
        bool canConnect,
        DatabaseCredentials credentials,
        SqlDialect dialect,
        Version version,
        ServerVersion serverVersion,
        string catalog,
        SchemaMapping[] schemaMappings,
        ILogger logger)
    {
        var otherInfo = new Dictionary<string, object>
        {
            { "DatabaseCredentials", credentials },
            { "DatabaseAvailable", canConnect }
        };

        SchemaDefaults serverDefaults;
        if (canConnect)
        {
            DbContextOptions<InfoSchemaContext> options = new DbContextOptionsBuilder<InfoSchemaContext>()
                .UseMySql(credentials.ConnectionString, serverVersion)
                .Options;

            using var context = new InfoSchemaContext(options);

            var defaultLoader = new MyDatabaseDefaultLoader(context);
            serverDefaults = defaultLoader.GetServerDefaults(SqlDialect.MySql, version);
            otherInfo.Add("ServerDefaults", serverDefaults);
            otherInfo.Add("CharacterSets", defaultLoader.GetCharacterSets(SqlDialect.MySql, version));
            otherInfo.Add("SchemaDefaults", GetAllSchemaDefaults(schemaMappings, serverDefaults, defaultLoader, catalog));
        }
        else
        {
            var defaultLoader = new FallbackDatabaseDefaultLoader(logger);
            serverDefaults = defaultLoader.GetServerDefaults(SqlDialect.MySql, version);
            otherInfo.Add("ServerDefaults", serverDefaults);
            otherInfo.Add("CharacterSets", defaultLoader.GetCharacterSets(SqlDialect.MySql, version));
            otherInfo.Add("SchemaDefaults", GetAllSchemaDefaults(schemaMappings, serverDefaults, defaultLoader, catalog));
        }

        return otherInfo;
    }

    protected static Dictionary<string, SchemaDefaults> GetAllSchemaDefaults(
        SchemaMapping[] rawMappings,
        SchemaDefaults serverDefaults,
        ILoadDatabaseDefaults defaultLoader,
        string catalogName)
    {
        var schemaDefaults = new Dictionary<string, SchemaDefaults>();
        foreach (SchemaMapping mapping in rawMappings)
        {
            if (mapping.SchemaDefaults != null)
            {
                schemaDefaults[mapping.SchemaName] = mapping.SchemaDefaults;
            }
            else
            {
                schemaDefaults[mapping.SchemaName] = defaultLoader.GetSchemaDefaults(mapping.SchemaName, catalogName, serverDefaults);
            }
        }
        return schemaDefaults;
    }

    protected override void RegisterServices(IHostApplicationBuilder builder, MyConfig config)
    {
        builder.Services.RegisterMySqlServices(config);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Could not connect to server. Falling back to default values. Attempted to connect using {Credentials}")]
    private static partial void s_logCantConnect(ILogger logger, string credentials, Exception ex);
}
