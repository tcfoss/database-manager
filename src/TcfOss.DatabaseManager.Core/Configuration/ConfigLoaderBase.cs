using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
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

    protected List<DeployScript> ExpandDeployScripts(IEnumerable<ConfigParsing.DeployScript> deployScripts, SchemaIdentifier schemaId, DirectoryInfo schemaRootPath, DirectoryInfo? parentRootPath, string? parentUniqueId = null, DeployScriptType? parentType = null)
    {
        var expandedDeployScripts = new List<DeployScript>();

        foreach (ConfigParsing.DeployScript deployScript in deployScripts)
        {
            FileInfo fullPath = ExtractDeployScriptPath(deployScript.FilePath, schemaRootPath, parentRootPath ?? schemaRootPath, maybeDirectory: true);
            string? uniqueId = null;
            DeployScriptType? scriptType = ExtractDeployScriptType(deployScript, parentType, optional: true);

            if (!string.IsNullOrWhiteSpace(deployScript.UniqueId))
            {
                uniqueId = deployScript.UniqueId;
            }
            else if (!string.IsNullOrWhiteSpace(parentUniqueId))
            {
                uniqueId = parentUniqueId;
            }

            if (fullPath.Attributes.HasFlag(FileAttributes.Directory))
            {
                expandedDeployScripts.AddRange(ExpandDeployScriptsFromDirectory(new DirectoryInfo(fullPath.FullName), schemaId, schemaRootPath, parentRootPath, uniqueId, scriptType));
            }
            else if (fullPath.Extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                     fullPath.Extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
            {
                expandedDeployScripts.AddRange(ExpandDeployScriptsFromYaml(fullPath, schemaId, schemaRootPath, fullPath.Directory ?? parentRootPath, uniqueId, scriptType));
            }
            else if (fullPath.Extension.Equals(".sql", StringComparison.OrdinalIgnoreCase))
            {
                DeployScriptType realType = ExtractDeployScriptType(deployScript, scriptType, false).Value;
                expandedDeployScripts.Add(new DeployScript()
                {
                    FilePath = fullPath,
                    Type = realType,
                    UniqueId = uniqueId,
                });
            }
            else
            {
                s_logSkippingNonSqlDeployScript(_logger, deployScript.FilePath, null);
            }
        }
        return expandedDeployScripts;
    }

    private List<DeployScript> ExpandDeployScriptsFromDirectory(DirectoryInfo directoryPath, SchemaIdentifier schemaId, DirectoryInfo schemaRootDirectory, DirectoryInfo? parentRootDirectory, string? parentUniqueId, DeployScriptType? parentType)
    {
        s_logSearchingForDeployScriptsInDirectory(_logger, directoryPath);

        return ExpandDeployScripts(
            Directory.EnumerateFiles(directoryPath.FullName, "*", SearchOption.AllDirectories)
            .OrderBy(subFile => subFile)
            .Select(subFile => new ConfigParsing.DeployScript
            {
                FilePath = subFile,
            }),
            schemaId,
            schemaRootDirectory,
            parentRootDirectory,
            parentUniqueId,
            parentType
        );
    }

    private List<DeployScript> ExpandDeployScriptsFromYaml(FileInfo yamlFile, SchemaIdentifier schemaId,
        DirectoryInfo schemaRootDirectory, DirectoryInfo? parentRootDirectory, string? parentUniqueId, DeployScriptType? parentType)
    {
        s_logReadingDeployScriptFromYaml(_logger, yamlFile);

        using var reader = new StreamReader(yamlFile.FullName);
        List<ConfigParsing.DeployScript> rawDeployScripts = Serialization.ParseConfig<List<ConfigParsing.DeployScript>>(reader, yamlFile.FullName);
        return ExpandDeployScripts(rawDeployScripts, schemaId, schemaRootDirectory, parentRootDirectory, parentUniqueId, parentType);
    }

    private static DeployScriptType? ExtractDeployScriptType(ConfigParsing.DeployScript deployScript, DeployScriptType? parentType, [DoesNotReturnIf(false)] bool optional)
    {
        if (deployScript.Type.HasValue)
        {
            if (Enum.TryParse(deployScript.Type.Value.ToString(), out DeployScriptType parsedType) && Enum.IsDefined(parsedType))
            {
                return parsedType;
            }
            throw new ConfigurationException.DeployScriptInvalidType(deployScript);
        }
        if (parentType.HasValue)
        {
            return parentType.Value;
        }
        if (optional)
        {
            return null;
        }
        throw new ConfigurationException.DeployScriptMissingType(deployScript);
    }

    private static FileInfo ExtractDeployScriptPath(string filePath, DirectoryInfo schemaRootPath, DirectoryInfo? parentRootPath = null, bool maybeDirectory = false)
    {
        FileInfo fileInfo;
        if (Path.IsPathRooted(filePath))
        {
            fileInfo = new FileInfo(filePath);
            if (Exists(fileInfo))
            {
                return fileInfo;
            }
        }

        if (parentRootPath != null)
        {
            fileInfo = new FileInfo(Path.Combine(parentRootPath.FullName, filePath));
            if (Exists(fileInfo))
            {
                return fileInfo;
            }
        }
        fileInfo = new FileInfo(Path.Combine(schemaRootPath.FullName, filePath));
        if (Exists(fileInfo))
        {
            return fileInfo;
        }

        throw new FileNotFoundException($"Deploy script file '{filePath}' not found in schema root or parent root.", filePath);

        bool Exists(FileInfo fileToCheck) => fileToCheck.Exists || (maybeDirectory && fileToCheck.Attributes.HasFlag(FileAttributes.Directory) && Directory.Exists(fileToCheck.FullName));
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

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Reading deploy script from yaml: {FilePath}")]
    private static partial void s_logReadingDeployScriptFromYaml(ILogger logger, FileInfo filePath);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Searching for deploy scripts in directory: {FilePath}")]
    private static partial void s_logSearchingForDeployScriptsInDirectory(ILogger logger, DirectoryInfo filePath);


    #endregion
}
