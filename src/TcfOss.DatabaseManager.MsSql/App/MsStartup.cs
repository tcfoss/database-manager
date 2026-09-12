using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.MsSql.App;

public partial class MsStartup : Startup<MsConfig>
{
    private const SqlDialect DefaultDialect = SqlDialect.MsSql;
    private const QuoteStyle DefaultQuoteStyle = QuoteStyle.Brackets;

    public override Config GetDefaultConfig(string rootDirectory)
    {
        return new Config
        {
            ProjectDirectory = rootDirectory,
            Catalog = "master",
            QuoteStyle = DefaultQuoteStyle,
            Dialect = DefaultDialect,
            Credentials = new Credentials
            {
                Hostname = "localhost",
                Port = "1433",
            },
            Schemas = [
                new SchemaMapping
                {
                    SchemaName = "dbo",
                    RootPath = "."
                }
            ]
        };
    }

    private static MsDatabaseCredentials ExtractDatabaseCredentials(Config config, IReadEnvironmentVariables environmentVariableReader)
    {
        if (config.Credentials == null)
        {
            throw new ConfigurationException.MissingFieldException("Credentials");
        }

        string? portString = config.Credentials.Port != null
            ? environmentVariableReader.SubstituteVariables(config.Credentials.Port)
            : null;

        int port;
        if (portString == null)
        {
            port = 1433;
        }
        else if (int.TryParse(portString, out port) && port is > 0 and <= 65535)
        {
            // Port is set correctly
        }
        else
        {
            throw new ConfigurationException.InvalidPortException(portString);
        }

        bool encrypt = true;
        if (config.Credentials.Encrypt.HasValue)
        {
            encrypt = config.Credentials.Encrypt.Value;
        }
        bool trustServerCertificate = false;
        if (config.Credentials.TrustServerCertificate.HasValue)
        {
            trustServerCertificate = config.Credentials.TrustServerCertificate.Value;
        }

        var credentials = new MsDatabaseCredentials
        {
            Host = environmentVariableReader.SubstituteVariables(config.Credentials.Hostname),
            Port = port,
            Instance = config.Credentials.Instance != null
                ? environmentVariableReader.SubstituteVariables(config.Credentials.Instance)
                : null,
            Username = config.Credentials.Username != null
                ? environmentVariableReader.SubstituteVariables(config.Credentials.Username)
                : null,
            Password = config.Credentials.Password != null
                ? environmentVariableReader.SubstituteVariables(config.Credentials.Password)
                : null,
            Encrypt = encrypt,
            TrustServerCertificate = trustServerCertificate,
            ConnectionTimeout = config.Credentials.ConnectionTimeout ?? DefaultTimeoutSeconds,
        };

        return credentials;
    }

    public override MsConfig BuildConfiguration(string rootDirectory, Config rawConfig, IReadEnvironmentVariables environmentVariableReader, bool relaxed, ILogger logger)
    {
        var loader = new MsConfigLoader(logger);
        bool canConnect = false;

        MsDatabaseCredentials credentials = ExtractDatabaseCredentials(rawConfig, environmentVariableReader);

        // TODO: attempt connection to resolve version and set canConnect = true.
        // For now we always fall back to the configured/default version.

        var otherInformation = new Dictionary<string, object>
        {
            { "DatabaseCredentials", credentials },
            { "DatabaseAvailable", canConnect },
        };

        MsConfig config = loader.LoadConfig(rootDirectory, rawConfig, otherInformation, relaxed);
        return config;
    }

    protected override void RegisterServices(IHostApplicationBuilder builder, MsConfig config)
    {
        builder.Services.RegisterMsSqlServices(config);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Could not connect to server. Falling back to default values. Attempted to connect using {Credentials}")]
    private static partial void s_logCantConnect(ILogger logger, string credentials, Exception ex);
}
