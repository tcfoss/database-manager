using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
// using TcfOss.DatabaseManager.Core.Configuration.Parsing.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Configuration;

public abstract partial class ConfigLoaderBase<TConfig, TSchemaMapping>(ILogger logger)
    : IConfigLoader<TConfig, TSchemaMapping>
    where TConfig : ConfigWithSchemaMapsBase<TSchemaMapping>
    where TSchemaMapping : ISchemaMapping
{
    private readonly ILogger _logger = logger;

    public abstract TConfig LoadConfig(string configDirectory, ConfigParsing.Config rawConfig, Dictionary<string, object> otherInformation, bool relaxed = false);

    protected abstract (SchemaIdentifier, TSchemaMapping) GetSchemaMapping(ConfigParsing.SchemaMapping rawSchema, CatalogIdentifier catalogId, QuoteStyle quoteStyle, string projectDirectory, Dictionary<string, object> otherInformation);

    protected Dictionary<SchemaIdentifier, TSchemaMapping> GetSchemaMappings(
        ConfigParsing.SchemaMapping[] rawSchemas,
        CatalogIdentifier catalogIdentifier,
        QuoteStyle quoteStyle,
        string projectDirectory,
        Dictionary<string, object> otherInformation
    )
    {
        var schemaMappings = new Dictionary<SchemaIdentifier, TSchemaMapping>();

        foreach (ConfigParsing.SchemaMapping rawSchema in rawSchemas)
        {
            (SchemaIdentifier schemaId, TSchemaMapping schemaMapping) = GetSchemaMapping(rawSchema, catalogIdentifier, quoteStyle, projectDirectory, otherInformation);
            schemaMappings[schemaId] = schemaMapping;
        }

        return schemaMappings;
    }

    protected List<DeployScript> ExpandDeployScripts(IEnumerable<DeployScript> deployScripts, string schemaRootPath, SchemaIdentifier schemaId, string? parentUniqueId = null)
    {
        var expandedDeployScripts = new List<DeployScript>();

        foreach (DeployScript deployScript in deployScripts)
        {
            deployScript.FilePath = deployScript.FilePath.GetAbsolutePath(schemaRootPath);

            if (string.IsNullOrWhiteSpace(deployScript.UniqueId))
            {
                deployScript.UniqueId = parentUniqueId;
            }

            if (Directory.Exists(deployScript.FilePath))
            {
                List<DeployScript> expandedScripts = ExpandDeployScriptsFromDirectory(deployScript.FilePath, schemaRootPath, schemaId, deployScript);
                expandedDeployScripts.AddRange(expandedScripts);
            }
            else if (deployScript.FilePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                     deployScript.FilePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                List<DeployScript> expandedScripts = ExpandDeployScriptsFromYaml(deployScript.FilePath, schemaRootPath, schemaId, deployScript.UniqueId);
                expandedDeployScripts.AddRange(expandedScripts);
            }
            else if (File.Exists(deployScript.FilePath) && deployScript.FilePath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse(deployScript.Type.ToString(), out DeployScriptType realType) || !Enum.IsDefined(realType))
                {
                    throw new ConfigurationException.DeployScriptMissingType(deployScript);
                }
                expandedDeployScripts.Add(deployScript with { Type = realType });
            }
            else if (!File.Exists(deployScript.FilePath))
            {
                throw new FileNotFoundException($"Deploy script file not found: {deployScript.FilePath}");
            }
            else
            {
                s_logSkippingNonSqlDeployScript(_logger, deployScript.FilePath, null);
            }
        }
        return expandedDeployScripts;
    }

    private List<DeployScript> ExpandDeployScriptsFromDirectory(string directoryPath, string schemaRootPath, SchemaIdentifier schemaId, DeployScript deployScript)
    {
        List<DeployScript> deployScripts = ExpandDeployScripts(
            Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .OrderBy(subFile => subFile)
            .Select(subFile => new DeployScript
            {
                Type = deployScript.Type,
                FilePath = subFile,
                UniqueId = deployScript.UniqueId
            }), schemaRootPath, schemaId);

        return deployScripts;
    }

    private List<DeployScript> ExpandDeployScriptsFromYaml(string yamlPath, string schemaRootPath, SchemaIdentifier schemaId, string? parentUniqueId = null)
    {
        using var reader = new StreamReader(yamlPath);
        List<DeployScript> rawDeployScripts = Serialization.ParseConfig<List<DeployScript>>(reader, yamlPath);
        return ExpandDeployScripts(rawDeployScripts, schemaRootPath, schemaId, parentUniqueId);
    }

    protected Refactor[] GetRefactors(SchemaIdentifier schemaId, string schemaRootPath, IReadOnlyCollection<string>? refactorFiles, QuoteStyle quoteStyle)
    {
        if (!refactorFiles.SafeAny())
        {
            return [];
        }

        refactorFiles = refactorFiles.Select(filePath => filePath.GetAbsolutePath(schemaRootPath)).ToArray();
        return GetRefactors(schemaId, refactorFiles, quoteStyle);
    }

    private Refactor[] GetRefactors(SchemaIdentifier schemaId, IEnumerable<string> refactorFiles, QuoteStyle quoteStyle)
    {
        List<Refactor> refactors = [];
        s_logBeginLoadingRefactors(_logger, null);

        foreach (string filePath in refactorFiles)
        {
            List<ConfigParsing.Refactor> rawRefactors = LoadRawRefactorsFromFile(filePath);
            refactors.AddRange(ConvertToRefactors(schemaId, rawRefactors, quoteStyle));
        }

        return [.. refactors];
    }

    private List<ConfigParsing.Refactor> LoadRawRefactorsFromFile(string filePath)
    {
        string fullFilePath = Path.GetFullPath(filePath);
        s_logLoadingRefactors(_logger, fullFilePath, null);

        List<ConfigParsing.Refactor> rawRefactors = Serialization.ParseConfig<List<ConfigParsing.Refactor>>(fullFilePath);
        s_logLoadedRefactors(_logger, fullFilePath, rawRefactors.Count, null);
        return rawRefactors;
    }

    private static List<Refactor> ConvertToRefactors(SchemaIdentifier schema, List<ConfigParsing.Refactor> rawRefactors, QuoteStyle quoteStyle)
    {
        var refactors = new List<Refactor>();

        foreach (ConfigParsing.Refactor raw in rawRefactors)
        {
            ObjectIdentifier? tableName = null;
            if (raw.TableName != null)
            {
                tableName = new ObjectIdentifier(raw.TableName, schema, quoteStyle);
            }

            Refactor refactor = raw.Type switch
            {
                ConfigParsing.Attributes.RefactorType.TableRename => new Refactor.TableRename(
                    raw.UniqueId,
                    schema,
                    new ObjectIdentifier(raw.OldName ?? throw new ConfigurationException.MissingFieldException(nameof(raw.OldName)), schema, quoteStyle),
                    new ObjectIdentifier(raw.NewName ?? throw new ConfigurationException.MissingFieldException(nameof(raw.NewName)), schema, quoteStyle)
                ),
                ConfigParsing.Attributes.RefactorType.ColumnRename => new Refactor.ColumnRename(
                    raw.UniqueId,
                    schema,
                    new ColumnIdentifier(raw.OldName ?? throw new ConfigurationException.MissingFieldException(nameof(raw.OldName)), tableName ?? throw new ConfigurationException.MissingFieldException(nameof(tableName)), quoteStyle),
                    new ColumnIdentifier(raw.NewName ?? throw new ConfigurationException.MissingFieldException(nameof(raw.NewName)), tableName ?? throw new ConfigurationException.MissingFieldException(nameof(tableName)), quoteStyle)
                ),
                _ => throw new NotSupportedException($"Unsupported refactor type: {raw.Type}")
            };
            refactors.Add(refactor);
        }

        return refactors;
    }

    private static uint? ApplyIntegerOverride(uint? baseValue, int? overrideValue)
    {
        if (overrideValue == null)
        {
            return null;
        }
        else if (overrideValue >= 0)
        {
            return (uint)overrideValue.Value;
        }
        else
        {
            return baseValue;
        }
    }

    protected static AttributeDefaults MergeAttributeDefaults(AttributeDefaults baseDefaults, ConfigParsing.AttributeDefaults overrideDefaults)
    {
        return new AttributeDefaults
        {
            NumericAttribute = overrideDefaults.NumericAttribute switch
            {
                ConfigParsing.Attributes.MySqlNumericAttribute.Signed => MySqlNumericAttribute.Signed,
                ConfigParsing.Attributes.MySqlNumericAttribute.Unsigned => MySqlNumericAttribute.Unsigned,
                ConfigParsing.Attributes.MySqlNumericAttribute.Zerofill => MySqlNumericAttribute.Zerofill,
                _ => baseDefaults.NumericAttribute
            },
            SignedIntWidth = ApplyIntegerOverride(baseDefaults.SignedIntWidth, overrideDefaults.SignedIntWidth),
            UnsignedIntWidth = ApplyIntegerOverride(baseDefaults.UnsignedIntWidth, overrideDefaults.UnsignedIntWidth),
            SignedBigIntWidth = ApplyIntegerOverride(baseDefaults.SignedBigIntWidth, overrideDefaults.SignedBigIntWidth),
            UnsignedBigIntWidth = ApplyIntegerOverride(baseDefaults.UnsignedBigIntWidth, overrideDefaults.UnsignedBigIntWidth),
            SignedMediumIntWidth = ApplyIntegerOverride(baseDefaults.SignedMediumIntWidth, overrideDefaults.SignedMediumIntWidth),
            UnsignedMediumIntWidth = ApplyIntegerOverride(baseDefaults.UnsignedMediumIntWidth, overrideDefaults.UnsignedMediumIntWidth),
            SignedSmallIntWidth = ApplyIntegerOverride(baseDefaults.SignedSmallIntWidth, overrideDefaults.SignedSmallIntWidth),
            UnsignedSmallIntWidth = ApplyIntegerOverride(baseDefaults.UnsignedSmallIntWidth, overrideDefaults.UnsignedSmallIntWidth),
            SignedTinyIntWidth = ApplyIntegerOverride(baseDefaults.SignedTinyIntWidth, overrideDefaults.SignedTinyIntWidth),
            UnsignedTinyIntWidth = ApplyIntegerOverride(baseDefaults.UnsignedTinyIntWidth, overrideDefaults.UnsignedTinyIntWidth),
            DecimalPrecision = GetNumericLengthFromPrecisionAndScale(overrideDefaults.DecimalPrecision, overrideDefaults.DecimalScale, baseDefaults.DecimalPrecision, "Decimal"),
            FloatPrecision = GetNumericLengthFromPrecisionAndScale(overrideDefaults.FloatPrecision, overrideDefaults.FloatScale, baseDefaults.FloatPrecision, "Float"),
            DoublePrecision = GetNumericLengthFromPrecisionAndScale(overrideDefaults.DoublePrecision, overrideDefaults.DoubleScale, baseDefaults.DoublePrecision, "Double"),
            YearPrecision = ApplyIntegerOverride(baseDefaults.YearPrecision, overrideDefaults.YearPrecision),
            IndexMethod = overrideDefaults.IndexMethod switch
            {
                ConfigParsing.Attributes.IndexMethod.Btree => IndexMethod.Btree,
                ConfigParsing.Attributes.IndexMethod.Hash => IndexMethod.Hash,
                ConfigParsing.Attributes.IndexMethod.Rtree => IndexMethod.Rtree,
                ConfigParsing.Attributes.IndexMethod.NotSet => baseDefaults.IndexMethod,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("IndexMethod", overrideDefaults.IndexMethod, ["Btree", "Hash", "Rtree", "NotSet"])
            },
            ForeignKeyOnUpdate = overrideDefaults.ForeignKeyOnUpdate switch
            {
                ConfigParsing.Attributes.ReferentialAction.NoAction => ReferentialAction.NoAction,
                ConfigParsing.Attributes.ReferentialAction.Restrict => ReferentialAction.Restrict,
                ConfigParsing.Attributes.ReferentialAction.Cascade => ReferentialAction.Cascade,
                ConfigParsing.Attributes.ReferentialAction.SetNull => ReferentialAction.SetNull,
                ConfigParsing.Attributes.ReferentialAction.SetDefault => ReferentialAction.SetDefault,
                ConfigParsing.Attributes.ReferentialAction.NotSet => baseDefaults.ForeignKeyOnUpdate,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("ForeignKeyOnUpdate", overrideDefaults.ForeignKeyOnUpdate, typeof(ConfigParsing.Attributes.ReferentialAction))
            },
            ForeignKeyOnDelete = overrideDefaults.ForeignKeyOnDelete switch
            {
                ConfigParsing.Attributes.ReferentialAction.NoAction => ReferentialAction.NoAction,
                ConfigParsing.Attributes.ReferentialAction.Restrict => ReferentialAction.Restrict,
                ConfigParsing.Attributes.ReferentialAction.Cascade => ReferentialAction.Cascade,
                ConfigParsing.Attributes.ReferentialAction.SetNull => ReferentialAction.SetNull,
                ConfigParsing.Attributes.ReferentialAction.SetDefault => ReferentialAction.SetDefault,
                ConfigParsing.Attributes.ReferentialAction.NotSet => baseDefaults.ForeignKeyOnDelete,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("ForeignKeyOnDelete", overrideDefaults.ForeignKeyOnDelete, typeof(ConfigParsing.Attributes.ReferentialAction))
            },
            FunctionDataRelation = overrideDefaults.FunctionDataRelation switch
            {
                ConfigParsing.Attributes.SqlDataRelation.ContainsSql => SqlDataRelation.ContainsSql,
                ConfigParsing.Attributes.SqlDataRelation.NoSql => SqlDataRelation.NoSql,
                ConfigParsing.Attributes.SqlDataRelation.ReadsSqlData => SqlDataRelation.ReadsSqlData,
                ConfigParsing.Attributes.SqlDataRelation.ModifiesSqlData => SqlDataRelation.ModifiesSqlData,
                ConfigParsing.Attributes.SqlDataRelation.NotSet => baseDefaults.FunctionDataRelation,
                null => null,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("FunctionDataRelation", overrideDefaults.FunctionDataRelation, typeof(ConfigParsing.Attributes.SqlDataRelation))
            },
            ProcedureDataRelation = overrideDefaults.ProcedureDataRelation switch
            {
                ConfigParsing.Attributes.SqlDataRelation.ContainsSql => SqlDataRelation.ContainsSql,
                ConfigParsing.Attributes.SqlDataRelation.NoSql => SqlDataRelation.NoSql,
                ConfigParsing.Attributes.SqlDataRelation.ReadsSqlData => SqlDataRelation.ReadsSqlData,
                ConfigParsing.Attributes.SqlDataRelation.ModifiesSqlData => SqlDataRelation.ModifiesSqlData,
                ConfigParsing.Attributes.SqlDataRelation.NotSet => baseDefaults.ProcedureDataRelation,
                null => null,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("ProcedureDataRelation", overrideDefaults.ProcedureDataRelation, typeof(ConfigParsing.Attributes.SqlDataRelation))
            },
            FunctionSecurityContext = overrideDefaults.FunctionSecurityContext switch
            {
                ConfigParsing.Attributes.SecurityContext.Definer => SecurityContext.Definer,
                ConfigParsing.Attributes.SecurityContext.Invoker => SecurityContext.Invoker,
                ConfigParsing.Attributes.SecurityContext.NotSet => baseDefaults.FunctionSecurityContext,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("FunctionSecurityContext", overrideDefaults.FunctionSecurityContext, typeof(ConfigParsing.Attributes.SecurityContext))
            },
            ProcedureSecurityContext = overrideDefaults.ProcedureSecurityContext switch
            {
                ConfigParsing.Attributes.SecurityContext.Definer => SecurityContext.Definer,
                ConfigParsing.Attributes.SecurityContext.Invoker => SecurityContext.Invoker,
                ConfigParsing.Attributes.SecurityContext.NotSet => baseDefaults.ProcedureSecurityContext,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("ProcedureSecurityContext", overrideDefaults.ProcedureSecurityContext, typeof(ConfigParsing.Attributes.SecurityContext))
            },
            ViewSecurityContext = overrideDefaults.ViewSecurityContext switch
            {
                ConfigParsing.Attributes.SecurityContext.Definer => SecurityContext.Definer,
                ConfigParsing.Attributes.SecurityContext.Invoker => SecurityContext.Invoker,
                ConfigParsing.Attributes.SecurityContext.NotSet => baseDefaults.ViewSecurityContext,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("ViewSecurityContext", overrideDefaults.ViewSecurityContext, typeof(ConfigParsing.Attributes.SecurityContext))
            },
            FunctionParameterDirection = overrideDefaults.FunctionParameterDirection switch
            {
                ConfigParsing.Attributes.RoutineParameterDirection.In => RoutineParameterDirection.In,
                ConfigParsing.Attributes.RoutineParameterDirection.Out => RoutineParameterDirection.Out,
                ConfigParsing.Attributes.RoutineParameterDirection.InOut => RoutineParameterDirection.InOut,
                ConfigParsing.Attributes.RoutineParameterDirection.NotSet => baseDefaults.FunctionParameterDirection,
                null => null,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("FunctionParameterDirection", overrideDefaults.FunctionParameterDirection, typeof(ConfigParsing.Attributes.RoutineParameterDirection))
            },
            ProcedureParameterDirection = overrideDefaults.ProcedureParameterDirection switch
            {
                ConfigParsing.Attributes.RoutineParameterDirection.In => RoutineParameterDirection.In,
                ConfigParsing.Attributes.RoutineParameterDirection.Out => RoutineParameterDirection.Out,
                ConfigParsing.Attributes.RoutineParameterDirection.InOut => RoutineParameterDirection.InOut,
                ConfigParsing.Attributes.RoutineParameterDirection.NotSet => baseDefaults.ProcedureParameterDirection,
                null => null,
                _ => throw new ConfigurationException.InvalidFieldOneOfException("ProcedureParameterDirection", overrideDefaults.ProcedureParameterDirection, typeof(ConfigParsing.Attributes.RoutineParameterDirection))
            },
            EventEnabledStatus = overrideDefaults.EventEnabledStatus ?? baseDefaults.EventEnabledStatus
        };
    }

    protected static DifferFormattingSettings MergeDifferFormattingSettings(DifferFormattingSettings baseSettings, ConfigParsing.DifferFormattingSettings? overrideSettings)
    {
        if (overrideSettings == null)
        {
            return baseSettings;
        }

        return new DifferFormattingSettings
        {
            ObjectNamePrefixWithSchema = overrideSettings.ObjectNamePrefixWithSchema ?? baseSettings.ObjectNamePrefixWithSchema,
            OmitModifiersIfDefault = overrideSettings.OmitModifiersIfDefault ?? baseSettings.OmitModifiersIfDefault,

            ProcedurePreferRawText = overrideSettings.ProcedurePreferRawText ?? overrideSettings.PreferRawText ?? baseSettings.ProcedurePreferRawText,
            FunctionPreferRawText = overrideSettings.FunctionPreferRawText ?? overrideSettings.PreferRawText ?? baseSettings.FunctionPreferRawText,
            TriggerPreferRawText = overrideSettings.TriggerPreferRawText ?? overrideSettings.PreferRawText ?? baseSettings.TriggerPreferRawText,
            ViewPreferRawText = overrideSettings.ViewPreferRawText ?? overrideSettings.PreferRawText ?? baseSettings.ViewPreferRawText,
            EventPreferRawText = overrideSettings.EventPreferRawText ?? overrideSettings.PreferRawText ?? baseSettings.EventPreferRawText,
            ProcedurePreferRawTextInScript = overrideSettings.ProcedurePreferRawTextInScript
                ?? overrideSettings.PreferRawTextInScript
                ?? overrideSettings.ProcedurePreferRawText
                ?? overrideSettings.PreferRawText
                ?? baseSettings.ProcedurePreferRawTextInScript,
            FunctionPreferRawTextInScript = overrideSettings.FunctionPreferRawTextInScript
                ?? overrideSettings.PreferRawTextInScript
                ?? overrideSettings.FunctionPreferRawText
                ?? overrideSettings.PreferRawText
                ?? baseSettings.FunctionPreferRawTextInScript,
            TriggerPreferRawTextInScript = overrideSettings.TriggerPreferRawTextInScript
                ?? overrideSettings.PreferRawTextInScript
                ?? overrideSettings.TriggerPreferRawText
                ?? overrideSettings.PreferRawText
                ?? baseSettings.TriggerPreferRawTextInScript,
            ViewPreferRawTextInScript = overrideSettings.ViewPreferRawTextInScript
                ?? overrideSettings.PreferRawTextInScript
                ?? overrideSettings.ViewPreferRawText
                ?? overrideSettings.PreferRawText
                ?? baseSettings.ViewPreferRawTextInScript,
            EventPreferRawTextInScript = overrideSettings.EventPreferRawTextInScript
                ?? overrideSettings.PreferRawTextInScript
                ?? overrideSettings.EventPreferRawText
                ?? overrideSettings.PreferRawText
                ?? baseSettings.EventPreferRawTextInScript,

            ViewPreferNormalizedBody = overrideSettings.ViewPreferNormalizedBody ?? baseSettings.ViewPreferNormalizedBody,

            TerminateStatements = overrideSettings.TerminateStatements ?? baseSettings.TerminateStatements,
            UseDelimiterAroundPrograms = overrideSettings.UseDelimiterAroundPrograms ?? baseSettings.UseDelimiterAroundPrograms,
            UseDelimiterAroundViews = overrideSettings.UseDelimiterAroundViews ?? baseSettings.UseDelimiterAroundViews,

            EmitCommentsWithWeights = overrideSettings.EmitCommentsWithWeights ?? baseSettings.EmitCommentsWithWeights
        };
    }

    protected static NormalizationSettings MergeNormalizationSettings(NormalizationSettings baseSettings, ConfigParsing.NormalizationSettings? overrideSettings)
    {
        if (overrideSettings == null)
        {
            return baseSettings;
        }

        return new NormalizationSettings
        {
            IdentifierQuotationHandling = overrideSettings.IdentifierQuotationHandling ?? baseSettings.IdentifierQuotationHandling,
            CteDeclarationNameQuotationHandling = overrideSettings.CteDeclarationNameQuotationHandling ?? baseSettings.CteDeclarationNameQuotationHandling,
            AccountQuoteStyle = overrideSettings.AccountQuoteStyle ?? baseSettings.AccountQuoteStyle,
        };
    }


    private static NumericLength? GetNumericLengthFromPrecisionAndScale(int? precision, int? scale, NumericLength? defaultValue, string label)
    {
        int? semifinalPrec;
        int? semifinalScale;
        if (defaultValue is NumericLength.PrecisionScale basePrecScale)
        {
            semifinalPrec = (int)basePrecScale.Length;
            semifinalScale = (int)basePrecScale.Scale;
        }
        else if (defaultValue is NumericLength.Precision basePrec)
        {
            semifinalPrec = (int)basePrec.Length;
            semifinalScale = null;
        }
        else
        {
            semifinalPrec = null;
            semifinalScale = null;
        }

        if (precision == null)
        {
            semifinalPrec = null;
        }
        else if (precision >= 0)
        {
            semifinalPrec = precision;
        }

        if (scale == null)
        {
            semifinalScale = null;
        }
        else if (scale >= 0)
        {
            semifinalScale = scale;
        }

        NumericLength? finalPrecision;
        if (semifinalPrec == null && semifinalScale == null)
        {
            finalPrecision = null;
        }
        else if (semifinalPrec != null && semifinalScale == null)
        {
            finalPrecision = new NumericLength.Precision((uint)semifinalPrec.Value);
        }
        else if (semifinalPrec != null && semifinalScale != null)
        {
            if (semifinalScale > semifinalPrec)
            {
                throw new ConfigurationException.InvalidNumericScaleGreaterThanPrecision(label, semifinalPrec, semifinalScale);
            }
            finalPrecision = new NumericLength.PrecisionScale((uint)semifinalPrec.Value, (uint)semifinalScale.Value);
        }
        else
        {
            throw new ConfigurationException.InvalidNumericScaleMissingPrecision(label);
        }
        return finalPrecision;
    }

    #region Log Delegates
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Loading refactor configurations.")]
    private static partial void s_logBeginLoadingRefactors(ILogger logger, Exception? ex);

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to load refactors from file: {FilePath}")]
    private static partial void s_logFailedToLoadRefactors(ILogger logger, string filePath, Exception? ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Successfully loaded {Num} refactors from file: {FilePath}")]
    private static partial void s_logLoadedRefactors(ILogger logger, string filePath, int num, Exception? ex);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Loading refactors from file: {FilePath}")]
    private static partial void s_logLoadingRefactors(ILogger logger, string filePath, Exception? ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Skipping unsupported deploy script file type: {FilePath}")]
    private static partial void s_logSkippingNonSqlDeployScript(ILogger logger, string filePath, Exception? ex);

    #endregion
}
