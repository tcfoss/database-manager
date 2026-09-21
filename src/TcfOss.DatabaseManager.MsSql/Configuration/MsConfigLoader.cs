using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Configuration;

public class MsConfigLoader(ILogger logger)
    : ConfigLoaderBase<MsConfig, MsSchemaMapping>(logger)
{
    private Version DefaultVersion { get; } = new(16, 0, 0);

    public override MsConfig LoadConfig(
        string configDirectory,
        ConfigParsing.Config rawConfig,
        Dictionary<string, object> otherInformation,
        bool relaxed = false)
    {
        QuoteStyle quoteStyle = rawConfig.QuoteStyle ?? QuoteStyle.Brackets;

        if (string.IsNullOrWhiteSpace(rawConfig.Catalog))
        {
            throw new ConfigurationException.MissingFieldException("Catalog");
        }

        var catalogIdentifier = new CatalogIdentifier(rawConfig.Catalog, quoteStyle);
        string projectDirectory = (rawConfig.ProjectDirectory ?? configDirectory).GetAbsolutePath(configDirectory);

        MsDatabaseCredentials credentials = (MsDatabaseCredentials)otherInformation["DatabaseCredentials"];

        bool databaseAvailable = otherInformation.TryGetValue("DatabaseAvailable", out object? dbAvailValue) && dbAvailValue is true;

        Dictionary<SchemaIdentifier, MsSchemaMapping> schemaMappings = GetSchemaMappings(rawConfig.Schemas, catalogIdentifier, quoteStyle, projectDirectory, otherInformation);

        Version? version;
        if (!string.IsNullOrWhiteSpace(rawConfig.Version))
        {
            if (!Version.TryParse(rawConfig.Version, out version))
            {
                throw new ConfigurationException.InvalidFieldTypeException("Version", "Major.Minor.Patch");
            }
        }
        else
        {
            version = DefaultVersion;
        }

        DifferFormattingSettings differFormattingSettings = GetDifferFormattingDefaults();
        if (rawConfig.DifferFormatting != null)
        {
            differFormattingSettings = MergeDifferFormattingSettings(differFormattingSettings, rawConfig.DifferFormatting);
        }

        return new MsConfig
        {
            ProjectDirectory = projectDirectory,
            Catalog = catalogIdentifier,
            Dialect = rawConfig.Dialect,
            Version = version,
            QuoteStyle = quoteStyle,
            Schemas = schemaMappings,
            Formatting = rawConfig.Formatting ?? new(),
            DifferFormatting = differFormattingSettings,
            DatabaseCredentials = credentials,
            ValidationSettings = relaxed ? GetRelaxedValidationSettings() : GetValidationSettings(),
            AttributeDefaults = new(),
            NormalizationSettings = new(),
            NameHandling = NameHandling.Lowercase,
            Logging = rawConfig.Logging,
            DatabaseAvailable = databaseAvailable,
            MinPoolSize = rawConfig.MinPoolSize,
            MaxPoolSize = rawConfig.MaxPoolSize
        };
    }

    protected override (SchemaIdentifier, MsSchemaMapping) GetSchemaMapping(
        ConfigParsing.SchemaMapping rawSchema,
        CatalogIdentifier catalogId,
        QuoteStyle quoteStyle,
        string projectDirectory,
        Dictionary<string, object> otherInformation)
    {
        if (string.IsNullOrWhiteSpace(rawSchema.RootPath))
        {
            throw new ConfigurationException.MissingFieldException("Schema.RootPath");
        }
        if (string.IsNullOrWhiteSpace(rawSchema.SchemaName))
        {
            throw new ConfigurationException.MissingFieldException("Schema.SchemaName");
        }

        var schemaId = new SchemaIdentifier(rawSchema.SchemaName, catalogId, quoteStyle);
        string schemaRootPath = rawSchema.RootPath.GetAbsolutePath(projectDirectory);
        DeployScript[] deployScripts = [];

        if (rawSchema.DeployScripts.SafeAny())
        {
            deployScripts = [.. ExpandDeployScripts(rawSchema.DeployScripts, schemaId, new DirectoryInfo(schemaRootPath), null)];
        }

        var schemaMapping = new MsSchemaMapping
        {
            SchemaName = new SchemaIdentifier(rawSchema.SchemaName, catalogId, quoteStyle),
            RootPath = schemaRootPath,
            IncludeFilePatterns = rawSchema.IncludeFilePatterns,
            ExcludeFilePatterns = rawSchema.ExcludeFilePatterns,
            ExcludeDatabaseObjectNames = rawSchema.ExcludeDatabaseObjectNames,
            Refactors = GetRefactors(schemaId, schemaRootPath, rawSchema.RefactorFiles, quoteStyle),
            DeployScripts = deployScripts,
        };
        return (schemaId, schemaMapping);
    }

    private static ValidationSettings GetValidationSettings() => new();

    private static ValidationSettings GetRelaxedValidationSettings() => new();


    private static DifferFormattingSettings GetDifferFormattingDefaults()
    {
        return new DifferFormattingSettings
        {
            ObjectNamePrefixWithSchema = true,
            OmitModifiersIfDefault = true,
            ProcedurePreferRawText = true,
            FunctionPreferRawText = true,
            TriggerPreferRawText = true,
            ViewPreferRawText = true,
            EventPreferRawText = false, // Does not matter - No events in SQL Server.
            ViewPreferNormalizedBody = false,
            TerminateStatements = true,
            UseDelimiterAroundPrograms = false,
            UseDelimiterAroundViews = false,
            EmitCommentsWithWeights = false,
        };
    }
}
