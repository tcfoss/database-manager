using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.MsSql.Configuration;

public partial class MsConfigurationBuilder : ConfigurationBuilder<MsConfig>
{
    private const SqlDialect DefaultDialect = SqlDialect.MsSql;
    private const QuoteStyle DefaultQuoteStyle = QuoteStyle.Brackets;
    private const int StartupConnectionTimeoutSeconds = 2;

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
                ConnectionTimeout = DefaultTimeoutSeconds,
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

    private static MsDatabaseCredentials ExtractDatabaseCredentials(Config config, EnvironmentVariableReaderBase environmentVariableReader)
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
                : "",
            Password = config.Credentials.Password != null
                ? environmentVariableReader.SubstituteVariables(config.Credentials.Password)
                : "",
            Encrypt = encrypt,
            TrustServerCertificate = trustServerCertificate,
            ConnectionTimeout = config.Credentials.ConnectionTimeout ?? DefaultTimeoutSeconds,
        };

        return credentials;
    }

    public override MsConfig BuildConfiguration(string rootDirectory, Config rawConfig, EnvironmentVariableReaderBase environmentVariableReader, bool relaxed, ILogger logger)
    {
        var loader = new MsConfigLoader(logger);
        bool canConnect = false;

        MsDatabaseCredentials credentials = ExtractDatabaseCredentials(rawConfig, environmentVariableReader);

        Version version;
        try
        {
            if (credentials.Instance == null)
            {
                s_logDefaultInstance(logger, credentials.Host);
            }

            var connectionStringBuilder = new SqlConnectionStringBuilder(credentials.ConnectionString)
            {
                ConnectTimeout = credentials.ConnectionTimeout > 0
                    ? Math.Min(credentials.ConnectionTimeout, StartupConnectionTimeoutSeconds)
                    : StartupConnectionTimeoutSeconds,
            };

            s_logConnecting(logger, new LogDelegateExecutionWrapper<MsDatabaseCredentials>(credentials, c => c.GetRedactedConnectionString()), connectionStringBuilder.ConnectTimeout);

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(connectionStringBuilder.ConnectTimeout));
            using var connection = new SqlConnection(connectionStringBuilder.ConnectionString);
            connection.OpenAsync(cancellation.Token).GetAwaiter().GetResult();

            string serverVersion = connection.ServerVersion!;
            version = new Version(serverVersion);
            canConnect = true;
            s_logConnected(logger, credentials.DataSource, serverVersion);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SqlException or OperationCanceledException)
        {
            s_logConnectionAttemptFailed(logger, credentials.DataSource, ex);
            s_logCantConnect(logger, credentials.GetRedactedConnectionString(), ex);
            if (rawConfig.Version == null || !Version.TryParse(rawConfig.Version, out version!))
            {
                version = loader.DefaultVersion;
            }
        }

        var otherInformation = new Dictionary<string, object>
        {
            { "DatabaseCredentials", credentials },
            { "DatabaseAvailable", canConnect },
            { "Version", version },
        };

        MsConfig config = loader.LoadConfig(rootDirectory, rawConfig, otherInformation, relaxed);
        return config;
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Trace, Message = "No instance name supplied for {Host}; using the default instance.")]
    private static partial void s_logDefaultInstance(ILogger logger, string host);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Attempting startup connection using {Credentials} with a timeout of {TimeoutSeconds} seconds.")]
    private static partial void s_logConnecting(ILogger logger, LogDelegateExecutionWrapper<MsDatabaseCredentials> credentials, int timeoutSeconds);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Connected to {DataSource}, SQL Server version {ServerVersion}.")]
    private static partial void s_logConnected(ILogger logger, string dataSource, string serverVersion);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Startup connection attempt to {DataSource} failed.")]
    private static partial void s_logConnectionAttemptFailed(ILogger logger, string dataSource, Exception ex);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Could not connect to server. Falling back to default values. Attempted to connect using {Credentials}")]
    private static partial void s_logCantConnect(ILogger logger, string credentials, Exception ex);
}
