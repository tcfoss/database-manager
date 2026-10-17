using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.MariaDb.DatabaseComms.EntityFramework;
using TcfOss.DatabaseManager.MariaDb.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;

namespace TcfOss.DatabaseManager.MariaDb.Configuration;

public partial class MaConfigurationBuilder : MyConfigurationBuilder
{
    protected override SqlDialect DefaultDialect => SqlDialect.MariaDb;

    public override MyConfig BuildConfiguration(string rootDirectory, Config rawConfig, IReadEnvironmentVariables environmentVariableReader, bool relaxed, ILogger logger)
    {
        var loader = new MaConfigLoader(logger);

        DatabaseCredentials credentials = ExtractDatabaseCredentials(rawConfig, environmentVariableReader);

        ServerVersion? serverVersion = null;
        Version version;
        try
        {
            serverVersion = ServerVersion.AutoDetect(credentials.ConnectionString);
            version = serverVersion.Version;
        }
        catch (MySqlConnector.MySqlException ex)
        {
            string credentialsString = rawConfig.Logging.EnableSensitiveDataLogging
                ? credentials.ConnectionString
                : credentials.GetRedactedConnectionString();
            s_logCantConnect(logger, credentialsString, ex);
            if (rawConfig.Version == null || !Version.TryParse(rawConfig.Version, out version!))
            {
                version = loader.DefaultVersion;
            }
        }

        Dictionary<string, object> otherInformation = GetOtherInformation(credentials, SqlDialect.MariaDb, version, serverVersion, rawConfig.Catalog ?? "def", rawConfig.Schemas, logger);

        MyConfig config = loader.LoadConfig(
            rootDirectory,
            rawConfig,
            otherInformation,
            relaxed
        );

        return config;
    }

    protected override Dictionary<string, object> GetOtherInformation(DatabaseCredentials credentials, SqlDialect dialect, Version version, ServerVersion? serverVersion, string catalog, SchemaMapping[] schemaMappings, ILogger logger)
    {
        var otherInfo = new Dictionary<string, object>
        {
            { "DatabaseCredentials", credentials },
            { "DatabaseAvailable", serverVersion != null },
            { "Version", version }
        };

        SchemaDefaults serverDefaults;
        if (serverVersion != null)
        {
            DbContextOptions<InfoSchemaContext> options = new DbContextOptionsBuilder<InfoSchemaContext>()
                .UseMySql(credentials.ConnectionString, serverVersion)
                .Options;

            using var context = new InfoSchemaContext(options);

            var defaultLoader = new MaDatabaseDefaultLoader(context);
            serverDefaults = defaultLoader.GetServerDefaults(SqlDialect.MariaDb, version);
            otherInfo.Add("ServerDefaults", serverDefaults);
            otherInfo.Add("CharacterSets", defaultLoader.GetCharacterSets(SqlDialect.MariaDb, version));
            otherInfo.Add("SchemaDefaults", GetAllSchemaDefaults(schemaMappings, serverDefaults, defaultLoader, catalog));

        }
        else
        {
            var defaultLoader = new FallbackDatabaseDefaultLoader(logger);
            serverDefaults = defaultLoader.GetServerDefaults(SqlDialect.MariaDb, version);
            otherInfo.Add("ServerDefaults", serverDefaults);
            otherInfo.Add("CharacterSets", defaultLoader.GetCharacterSets(SqlDialect.MariaDb, version));
            otherInfo.Add("SchemaDefaults", GetAllSchemaDefaults(schemaMappings, serverDefaults, defaultLoader, catalog));
        }

        return otherInfo;
    }


    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Could not connect to server. Falling back to default values. Attempted to connect using {Credentials}")]
    private static partial void s_logCantConnect(ILogger logger, string credentials, Exception ex);
}
