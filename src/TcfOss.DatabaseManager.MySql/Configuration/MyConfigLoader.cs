using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MySql.Configuration;

public class MyConfigLoader(ILogger logger)
    : ConfigLoaderBase<MyConfig, MySchemaMapping>(logger), IConfigLoader<MyConfig, MySchemaMapping>
{
    public virtual Version DefaultVersion { get; } = new(8, 4, 6);

    public override MyConfig LoadConfig(
        string configDirectory,
        ConfigParsing.Config rawConfig,
        Dictionary<string, object> otherInformation,
        bool relaxed = false)
    {
        QuoteStyle quoteStyle = rawConfig.QuoteStyle ?? QuoteStyle.Backticks;
        var catalogIdentifier = new CatalogIdentifier(rawConfig.Catalog ?? "def", quoteStyle);
        string projectDirectory = (rawConfig.ProjectDirectory ?? configDirectory).GetAbsolutePath(configDirectory);

        SchemaDefaults serverDefaults = (SchemaDefaults)otherInformation["ServerDefaults"];

        Dictionary<string, CharacterSetSpec> characterSets = (Dictionary<string, CharacterSetSpec>)otherInformation["CharacterSets"];

        DatabaseCredentials credentials = (DatabaseCredentials)otherInformation["DatabaseCredentials"];

        bool databaseAvailable = otherInformation.TryGetValue("DatabaseAvailable", out object? dbAvailValue) && dbAvailValue is true;

        Dictionary<SchemaIdentifier, MySchemaMapping> schemaMappings = GetSchemaMappings(rawConfig.Schemas, catalogIdentifier, quoteStyle, projectDirectory, otherInformation);

        AttributeDefaults attributeDefaults = GetAttributeDefaults();
        if (rawConfig.AttributeDefaults != null)
        {
            attributeDefaults = MergeAttributeDefaults(attributeDefaults, rawConfig.AttributeDefaults);
        }

        ParseSettings parseSettings;
        if (rawConfig.ParseSettings != null)
        {
            parseSettings = rawConfig.ParseSettings;
        }
        else
        {
            parseSettings = new ParseSettings();
            if (rawConfig.Dialect == SqlDialect.MySql)
            {
                parseSettings.RemoveSlashesBeforeQuotesGenerationExpression = true;
            }
        }

        NormalizationSettings viewNormalizationSettings = GetViewNormalizationDefaults();
        if (rawConfig.ViewNormalizationSettings != null)
        {
            viewNormalizationSettings = MergeNormalizationSettings(viewNormalizationSettings, rawConfig.ViewNormalizationSettings);
        }

        DifferFormattingSettings differFormattingSettings = GetDifferFormattingDefaults();
        if (rawConfig.DifferFormatting != null)
        {
            differFormattingSettings = MergeDifferFormattingSettings(differFormattingSettings, rawConfig.DifferFormatting);
        }

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

        return new MyConfig
        {
            ProjectDirectory = projectDirectory,
            Catalog = catalogIdentifier,
            Dialect = rawConfig.Dialect,
            Version = version,
            QuoteStyle = quoteStyle,
            ServerDefaults = serverDefaults,
            Schemas = schemaMappings,
            CharacterSets = characterSets,
            Formatting = rawConfig.Formatting ?? new(),
            DifferFormatting = differFormattingSettings,
            DefaultDefiner = CreateDefiner(rawConfig.DefaultDefinerAccount, rawConfig.DefaultDefinerHost),
            DatabaseCredentials = credentials,
            ValidationSettings = relaxed ? GetRelaxedValidationSettings() : GetValidationSettings(),
            AttributeDefaults = attributeDefaults,
            ParseSettings = parseSettings,
            NormalizationSettings = viewNormalizationSettings,
            NameHandling = NameHandling.Lowercase,
            Logging = rawConfig.Logging,
            DatabaseAvailable = databaseAvailable,
            MinPoolSize = rawConfig.MinPoolSize,
            MaxPoolSize = rawConfig.MaxPoolSize
        };
    }

    protected override (SchemaIdentifier, MySchemaMapping) GetSchemaMapping(ConfigParsing.SchemaMapping rawSchema, CatalogIdentifier catalogId, QuoteStyle quoteStyle, string projectDirectory, Dictionary<string, object> otherInformation)
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
            deployScripts = [.. ExpandDeployScripts(
                rawSchema.DeployScripts,
                schemaId,
                new DirectoryInfo(schemaRootPath),
                null
            )];
        }

        SchemaDefaults schemaDefaults = ((Dictionary<string, SchemaDefaults>)otherInformation["SchemaDefaults"])[rawSchema.SchemaName];

        var schemaMapping = new MySchemaMapping
        {
            SchemaName = new SchemaIdentifier(rawSchema.SchemaName, catalogId, quoteStyle),
            RootPath = schemaRootPath,
            IncludeFilePatterns = rawSchema.IncludeFilePatterns,
            ExcludeFilePatterns = rawSchema.ExcludeFilePatterns,
            ExcludeDatabaseObjectNames = rawSchema.ExcludeDatabaseObjectNames,
            Refactors = GetRefactors(schemaId, schemaRootPath, rawSchema.RefactorFiles, quoteStyle),
            DeployScripts = deployScripts,
            SchemaDefaults = schemaDefaults,
        };
        return (schemaId, schemaMapping);
    }

    protected virtual AttributeDefaults GetAttributeDefaults()
    {
        return new AttributeDefaults
        {
            NumericAttribute = MySqlNumericAttribute.Signed,
            SignedIntWidth = null,
            UnsignedIntWidth = null,
            SignedBigIntWidth = null,
            UnsignedBigIntWidth = null,
            SignedMediumIntWidth = null,
            UnsignedMediumIntWidth = null,
            SignedSmallIntWidth = null,
            UnsignedSmallIntWidth = null,
            SignedTinyIntWidth = null,
            UnsignedTinyIntWidth = null,
            DecimalPrecision = new NumericLength.PrecisionScale(10, 0),
            FloatPrecision = null,
            DoublePrecision = null,
            YearPrecision = null,
            ForeignKeyOnDelete = ReferentialAction.NoAction,
            ForeignKeyOnUpdate = ReferentialAction.NoAction,
            FunctionDataRelation = SqlDataRelation.ContainsSql,
            ProcedureDataRelation = SqlDataRelation.ContainsSql,
            FunctionParameterDirection = null,
            ProcedureParameterDirection = RoutineParameterDirection.In,
        };
    }

    protected virtual NormalizationSettings GetViewNormalizationDefaults()
    {
        return new NormalizationSettings
        {
            IdentifierQuotationHandling = IdentifierQuotationHandling.Always,
            CteDeclarationNameQuotationHandling = IdentifierQuotationHandling.Always,
            AccountQuoteStyle = ExtendedQuoteStyle.Backticks,
            CastConvertVarcharToChar = true,
            CastAddCharsetToType = true,
        };
    }

    private static DifferFormattingSettings GetDifferFormattingDefaults()
    {
        return new DifferFormattingSettings
        {
            ObjectNamePrefixWithSchema = false,
            OmitModifiersIfDefault = true,
            ProcedurePreferRawText = true,
            FunctionPreferRawText = true,
            TriggerPreferRawText = true,
            ViewPreferRawText = true,
            ViewPreferNormalizedBody = false,
            EventPreferRawText = true,
            TerminateStatements = true,
            UseDelimiterAroundPrograms = true,
            UseDelimiterAroundViews = false,
            EmitCommentsWithWeights = false,
        };
    }

    protected virtual ValidationSettings GetValidationSettings()
    {
        return new ValidationSettings()
        {
            AllowCheckOnColumn = false,
            AllowNamedColumnDefault = false,
            AllowNamedColumnUnique = false,
            AllowNamedColumnNullability = false,
            AllowNamedColumnCheck = false,
        };
    }

    private static ValidationSettings GetRelaxedValidationSettings()
    {
        return new ValidationSettings
        {
            AllowUniqueOnColumn = true,
            AllowCheckOnColumn = true,

            AllowCreateTableAsSelect = true,
            AllowCreateTableNoColumns = true,
            AllowImplicitDefiner = true,
            AllowMissingDefiner = true,
            AllowMissingSecurityContext = true,
        };
    }

    private static Definer? CreateDefiner(string? definerAccount, string? definerHost)
    {
        definerAccount = string.IsNullOrWhiteSpace(definerAccount) ? null : definerAccount.Trim();
        definerHost = string.IsNullOrWhiteSpace(definerHost) ? null : definerHost.Trim();

        if (definerAccount == null)
        {
            if (definerHost != null)
            {
                throw new ConfigurationException.DefinerHostWithoutAccount();
            }
            return null;
        }

        if (definerHost != null)
        {
            return new Definer(new Account.IdentityWithHost(ConstructIdentifier(definerAccount), ConstructIdentifier(definerHost)));
        }
        return new Definer(new Account.Identity(ConstructIdentifier(definerAccount)));
    }

    private static ExtendedIdentifier ConstructIdentifier(string rawIdentifier)
    {
        if (rawIdentifier.StartsWith("CURRENT_USER", StringComparison.OrdinalIgnoreCase) ||
            rawIdentifier.StartsWith("CURRENT_ROLE", StringComparison.OrdinalIgnoreCase) ||
            rawIdentifier.StartsWith("SESSION_USER ", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationException.ImplicitDefiner();
        }

        ExtendedQuoteStyle quote = ExtendedQuoteStyle.None;
        if (rawIdentifier.StartsWith('\''))
        {
            rawIdentifier = rawIdentifier.Trim('\'');
            quote = ExtendedQuoteStyle.SingleQuote;
        }
        else if (rawIdentifier.StartsWith('\"'))
        {
            rawIdentifier = rawIdentifier.Trim('\"');
            quote = ExtendedQuoteStyle.Ansi;
        }
        else if (rawIdentifier.StartsWith('`'))
        {
            rawIdentifier = rawIdentifier.Trim('`');
            quote = ExtendedQuoteStyle.Backticks;
        }
        else if (rawIdentifier.StartsWith('['))
        {
            rawIdentifier = rawIdentifier.Trim('[', ']');
            quote = ExtendedQuoteStyle.Brackets;
        }

        return new ExtendedIdentifier(rawIdentifier, quote);
    }
}
