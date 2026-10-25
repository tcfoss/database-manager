using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Configuration;

public class GenericConfigLoader(ILogger logger, IFileSystem? fileSystem = null)
    : ConfigLoaderBase<GenericConfig, SchemaMappingBase>(logger, fileSystem)
{
    public override GenericConfig LoadConfig(
        string configDirectory,
        ConfigParsing.Config rawConfig,
        Dictionary<string, object> otherInformation,
        bool relaxed = false
    )
    {
        QuoteStyle quoteStyle = rawConfig.QuoteStyle ?? QuoteStyle.Ansi;
        var catalogIdentifier = new CatalogIdentifier(rawConfig.Catalog ?? "def", quoteStyle);
        string projectDirectory = (rawConfig.ProjectDirectory ?? configDirectory).GetAbsolutePath(configDirectory);

        Dictionary<SchemaIdentifier, SchemaMappingBase> schemaMappings = GetSchemaMappings(rawConfig.Schemas, catalogIdentifier, quoteStyle, projectDirectory, otherInformation);

        NormalizationSettings viewNormalizationSettings = new();
        if (rawConfig.ViewNormalizationSettings != null)
        {
            viewNormalizationSettings = MergeNormalizationSettings(viewNormalizationSettings, rawConfig.ViewNormalizationSettings);
        }

        DifferFormattingSettings differFormattingSettings = new();
        if (rawConfig.DifferFormatting != null)
        {
            differFormattingSettings = MergeDifferFormattingSettings(differFormattingSettings, rawConfig.DifferFormatting);
        }

        AttributeDefaults attributeDefaults = new();
        if (rawConfig.AttributeDefaults != null)
        {
            attributeDefaults = MergeAttributeDefaults(attributeDefaults, rawConfig.AttributeDefaults);
        }

        return new GenericConfig
        {
            ProjectDirectory = projectDirectory,
            Catalog = catalogIdentifier,
            Dialect = rawConfig.Dialect,
            QuoteStyle = quoteStyle,
            Schemas = schemaMappings,
            Formatting = rawConfig.Formatting ?? new(),
            DifferFormatting = differFormattingSettings,
            AttributeDefaults = attributeDefaults,
            Logging = rawConfig.Logging,
            ValidationSettings = GetValidationSettings(),
            NormalizationSettings = viewNormalizationSettings,
            DatabaseAvailable = false,
        };
    }

    protected override (SchemaIdentifier, SchemaMappingBase) GetSchemaMapping(
        ConfigParsing.SchemaMapping rawSchema,
        CatalogIdentifier catalogId,
        QuoteStyle quoteStyle,
        string projectDirectory,
        Dictionary<string, object>? otherInformation
    )
    {
        if (string.IsNullOrWhiteSpace(rawSchema.RootPath))
        {
            throw new ConfigurationException.MissingFieldException("Schema.RootPath.");
        }
        if (string.IsNullOrWhiteSpace(rawSchema.SchemaName))
        {
            throw new ConfigurationException.MissingFieldException("Schema.SchemaName");
        }

        var schemaId = new SchemaIdentifier(rawSchema.SchemaName, catalogId, quoteStyle);
        string schemaRootPath = rawSchema.RootPath.GetAbsolutePath(projectDirectory);
        DeployScript[] deployScripts = GetDeployScripts(rawSchema.DeployScripts, schemaId, new DirectoryInfo(schemaRootPath));

        var schemaMapping = new SchemaMappingBase
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

    private static ValidationSettings GetValidationSettings()
    {
        return new ValidationSettings()
        {
            AllowUniqueOnColumn = true,
            AllowCheckOnColumn = true,
        };
    }
}
