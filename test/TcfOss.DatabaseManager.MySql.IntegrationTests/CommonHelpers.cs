using System.Text;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;
using TcfOss.DatabaseManager.MariaDb.App;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;
using Testcontainers.MariaDb;
using Testcontainers.MySql;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests;

public static partial class CommonHelpers
{
    private static readonly Regex s_hostnameRegex = GetHostnameRegex();
    private static readonly Regex s_dialectRegex = GetDialectRegex();
    private static readonly Regex s_usernameRegex = GetUsernameRegex();
    private static readonly Regex s_portRegex = GetPortRegex();
    private static readonly Regex s_passwordRegex = GetPasswordRegex();

    private static DirectoryInfo SchemasDirectory { get; } = new(Path.Combine(CommonDirectoryPath.GetProjectDirectory().DirectoryPath, "..", "Resources", "TestSchemas", "MySql"));
    public static DirectoryInfo InitialSourceDirectory { get; } = new(Path.Combine(SchemasDirectory.FullName, "Initial"));
    public static DirectoryInfo SimpleSchemaSourceDirectory { get; } = new(Path.Combine(SchemasDirectory.FullName, "SimpleSchema"));

    public static DirectoryInfo GetSchemaDirectory(string testSchemaName)
    {
        return new DirectoryInfo(Path.Combine(SchemasDirectory.FullName, testSchemaName));
    }

    public static DirectoryInfo GetSchemaDirectory(params string[] pathSegments)
    {
        return new DirectoryInfo(Path.Combine([SchemasDirectory.FullName, .. pathSegments]));
    }

    public static async Task InitializeLibrarySchema(this IDatabaseContainer container)
    {
        await container.ExecAsync(["sh", "/InitialSchemas/Init/initialize_library.sh"]);
    }

    public static ushort GetPort(this IDatabaseContainer container)
    {
        return container switch
        {
            MySqlContainer => MySqlBuilder.MySqlPort,
            MariaDbContainer => MariaDbBuilder.MariaDbPort,
            FakeDbContainer => FakeDbBuilder.FakeDbPort,
            _ => throw new NotSupportedException($"Container type {container.GetType().Name} is not supported."),
        };
    }

    public static MyConfig GetLibrarySchemaConfig(this IDatabaseContainer container, string rootPath, bool includeScripts, bool includeRefactors, ushort port, SqlDialect dialect = SqlDialect.MariaDb, bool removeSlashesBeforeQuotesGenerationExpression = false, IReadEnvironmentVariables? environmentVariableReader = null, bool objectNamePrefixWithSchema = true)
    {
        DeployScript[] catalogScripts = [];
        DeployScript[] activityScripts = [];
        string[] catalogRefactors = [];
        string[] activityRefactors = [];
        string[] activityExclusions = [];
        if (includeScripts)
        {
            catalogScripts = [
                new DeployScript()
                {
                    Type = Core.DatabaseComms.DeployScriptType.PreDeployment,
                    FilePath = "../Scripts/01_preserve_book_info.sql",
                    UniqueId = "00000000-0000-0000-0000-000000000010",
                },
                new DeployScript()
                {
                    Type = Core.DatabaseComms.DeployScriptType.PreDeployment,
                    FilePath = "../Scripts/02_preserve_active_rental.sql",
                    UniqueId = "00000000-0000-0000-0000-000000000011",
                },
                new DeployScript()
                {
                    Type = Core.DatabaseComms.DeployScriptType.PreAddConstraints,
                    FilePath = "../Scripts/03_populate_item_status.sql",
                    UniqueId = "00000000-0000-0000-0000-000000000012",
                },
                new DeployScript()
                {
                    Type = Core.DatabaseComms.DeployScriptType.PreAddConstraints,
                    FilePath = "../Scripts/04_populate_acquisition.sql",
                    UniqueId = "00000000-0000-0000-0000-000000000013",
                },
                new DeployScript()
                {
                    Type = Core.DatabaseComms.DeployScriptType.PreAddConstraints,
                    FilePath = "../Scripts/05_populate_item.sql",
                    UniqueId = "00000000-0000-0000-0000-000000000014",
                }
            ];
            activityScripts = [
                new DeployScript()
                {
                    Type = Core.DatabaseComms.DeployScriptType.PreDeployment,
                    FilePath = "Tables/misplaced_script.sql",
                    UniqueId = "00000000-0000-0000-0000-000000000003",
                }
            ];
        }
        else
        {
            activityExclusions = ["Tables/misplaced_script.sql"];
        }

        if (includeRefactors)
        {
            catalogRefactors = ["refactors.yaml"];
            activityRefactors = ["refactors.yaml"];
        }

        var password = dialect switch
        {
            SqlDialect.MySql => "mysql",
            SqlDialect.MariaDb => "mariadb",
            SqlDialect.Generic => "generic",
            _ => throw new NotSupportedException($"Dialect {dialect} is not supported.")
        };

        var rawConfig = new ConfigParsing.Config
        {
            ProjectDirectory = rootPath,
            Catalog = "def",
            Dialect = dialect,
            QuoteStyle = QuoteStyle.Backticks,
            Credentials = new ConfigParsing.Credentials
            {
                Hostname = container.Hostname,
                Username = "root",
                Password = password,
                Port = container.GetMappedPublicPort(port).ToString(),
            },
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "library_catalog",
                    RootPath = "LibraryCatalog",
                    DeployScripts = catalogScripts,
                    RefactorFiles = catalogRefactors,
                },
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "library_identity",
                    RootPath = "LibraryIdentity",
                },
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "library_activity",
                    RootPath = "LibraryActivity",
                    ExcludeFilePatterns = activityExclusions,
                    DeployScripts = activityScripts,
                    RefactorFiles = activityRefactors,
                }
            ],
            Logging = new LogSettings()
            {
                DatabaseLogLevel = LogLevel.Information,
                Target = LogTarget.File,
            },
            DifferFormatting = new ConfigParsing.DifferFormattingSettings
            {
                ObjectNamePrefixWithSchema = objectNamePrefixWithSchema,
                OmitModifiersIfDefault = true,
                ViewPreferNormalizedBody = true,
            },
        };

        if (Path.Exists(Path.Combine(rootPath, "LibraryCatalog", "refactors.yaml")))
        {
            rawConfig.Schemas[0].RefactorFiles = ["refactors.yaml"];
        }
        if (Path.Exists(Path.Combine(rootPath, "LibraryIdentity", "refactors.yaml")))
        {
            rawConfig.Schemas[1].RefactorFiles = ["refactors.yaml"];
        }
        if (Path.Exists(Path.Combine(rootPath, "LibraryActivity", "refactors.yaml")))
        {
            rawConfig.Schemas[2].RefactorFiles = ["refactors.yaml"];
        }

        MyStartup startup;
        if (dialect == SqlDialect.MySql)
        {
            startup = new MyStartup();
        }
        else if (dialect == SqlDialect.MariaDb)
        {
            startup = new MaStartup();
        }
        else
        {
            throw new NotSupportedException($"Dialect {dialect} is not supported.");
        }

        MyConfig config = (MyConfig)startup.StartApp(
            rootPath,
            rawConfig,
            relaxed: false,
            environmentVariableReader: environmentVariableReader
        );

        config.ParseSettings.RemoveSlashesBeforeQuotesGenerationExpression = removeSlashesBeforeQuotesGenerationExpression;
        return config;
    }

    public static MyConfig GetSimpleSchemaConfig(this IDatabaseContainer container, string rootPath, ushort port, SqlDialect dialect)
    {
        var rawConfig = new ConfigParsing.Config
        {
            ProjectDirectory = rootPath,
            Catalog = "def",
            Dialect = dialect,
            QuoteStyle = QuoteStyle.Backticks,
            Credentials = new ConfigParsing.Credentials
            {
                Hostname = container.Hostname,
                Username = "root",
                Password = dialect == SqlDialect.MySql ? "mysql" : "mariadb",
                Port = container.GetMappedPublicPort(port).ToString(),
            },
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "simple_schema",
                    RootPath = "Schema",
                }
            ],
            Logging = new LogSettings
            {
                DatabaseLogLevel = LogLevel.Information,
                Target = LogTarget.File,
            },
            DifferFormatting = new ConfigParsing.DifferFormattingSettings
            {
                ObjectNamePrefixWithSchema = false,
                OmitModifiersIfDefault = true,
            },
        };

        MyStartup startup = dialect == SqlDialect.MySql ? new MyStartup() : new MaStartup();
        return (MyConfig)startup.StartApp(rootPath, rawConfig, relaxed: false);
    }

    public static MariaDbBuilder WithStandardOptions(this MariaDbBuilder builder)
    {
        return builder
            .WithName($"container_mariadb_{Guid.NewGuid():N}")
            .WithPrivileged(true)
            .WithCreateParameterModifier(p => p.HostConfig?.Init = true)
            .WithUsername("root")
            .WithDatabase("mariadb")
            .WithResourceMapping(InitialSourceDirectory, "/InitialSchemas");
    }

    public static MySqlBuilder WithStandardOptions(this MySqlBuilder builder)
    {
        return builder
            .WithName($"container_mysql_{Guid.NewGuid():N}")
            .WithPrivileged(true)
            .WithCreateParameterModifier(p => p.HostConfig?.Init = true)
            .WithUsername("root")
            .WithDatabase("mariadb")
            .WithResourceMapping(InitialSourceDirectory, "/InitialSchemas");
    }

    public static async Task<ExecResult> ExecScriptAsync(this IDatabaseContainer container, string script, CancellationToken ct)
    {
        script = $"SET NAMES 'utf8mb4';\n\n{script}";
        if (container is MySqlContainer mySqlContainer)
        {
            return await mySqlContainer.ExecScriptAsync(script, ct);
        }
        else if (container is MariaDbContainer mariaDbContainer)
        {
            return await mariaDbContainer.ExecScriptAsync(script, ct);
        }
        else
        {
            throw new NotSupportedException($"Container type {container.GetType().Name} is not supported.");
        }
    }

    public static string GetSimpleSchemaInitializationScript()
    {
        var initialRootDirectory = Path.Combine(SimpleSchemaSourceDirectory.FullName, "Initial");
        var schemaRootDirectory = Path.Combine(initialRootDirectory, "Schema");
        var dataRootDirectory = Path.Combine(initialRootDirectory, "Data");
        var sb = new StringBuilder();

        sb.AppendLine(
            "CREATE DATABASE IF NOT EXISTS `simple_schema` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;");
        sb.AppendLine();

        sb.AppendLine("USE `simple_schema`;");
        sb.AppendLine();

        // Include every table definition so new tables don't need a matching CommonHelpers change.
        foreach (var sqlFile in Directory.GetFiles(schemaRootDirectory, "*.sql").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(File.ReadAllText(sqlFile));
        }

        // Seed data lives separately from Schema so it isn't parsed as part of the schema definition.
        if (Directory.Exists(dataRootDirectory))
        {
            foreach (var sqlFile in Directory.GetFiles(dataRootDirectory, "*.sql").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(File.ReadAllText(sqlFile));
            }
        }

        return sb.ToString();
    }

    public static void UpdateConfiguration(SqlDialect dialect, string dirPath, string? host, ushort? port)
    {
        var text = File.ReadAllText(Path.Combine(dirPath, "database-manager.yaml"));

        if (!string.IsNullOrEmpty(host))
        {
            text = s_hostnameRegex.Replace(text, $"HostName: {host}");
        }
        var dialectRegex = s_dialectRegex;
        text = dialectRegex.Replace(text, $"Dialect: {dialect}");
        var usernameRegex = s_usernameRegex;
        text = usernameRegex.Replace(text, "UserName: root");
        if (port != null)
        {
            text = s_portRegex.Replace(text, $"Port: {port}");
        }

        var password = dialect switch
        {
            SqlDialect.MySql => "mysql",
            SqlDialect.MariaDb => "mariadb",
            SqlDialect.Generic => "generic",
            _ => throw new NotSupportedException($"Dialect {dialect} is not supported.")
        };

        var passwordRegex = s_passwordRegex;
        text = passwordRegex.Replace(text, $"Password: {password}");

        File.WriteAllText(Path.Combine(dirPath, "database-manager.yaml"), text);
    }


    [GeneratedRegex(@"HostName: \w+")]
    private static partial Regex GetHostnameRegex();

    [GeneratedRegex(@"Dialect: \w+")]
    private static partial Regex GetDialectRegex();
    [GeneratedRegex(@"UserName: \w+")]
    private static partial Regex GetUsernameRegex();
    [GeneratedRegex(@"Port: \d+")]
    private static partial Regex GetPortRegex();
    [GeneratedRegex(@"Password: \w+")]
    private static partial Regex GetPasswordRegex();
}
