using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgTableBuilder(
    ObjectIdentifier name,
    NameHandling nameHandling,
    INormalizeComponents componentNormalizer,
    AttributeDefaults attributeDefaults,
    PgServerDefaults serverDefaults,
    INormalizeSql sqlNormalizer,
    SourceRef? sourceRef)
    : TableBuilder<PgTable>(name, nameHandling, componentNormalizer, sourceRef)
{
    private readonly List<PreColumn> _columns = [];
    private readonly PgPartitionNormalizer _partitionNormalizer = new(componentNormalizer, sqlNormalizer);
    private readonly PgExclusionNormalizer _exclusionNormalizer = new(componentNormalizer, sqlNormalizer);
    private bool _unlogged;
    private Identifier? _accessMethod;
    private Identifier? _tablespace;
    private PgPrimaryKey? _primaryKey;
    private readonly DatabaseComponentDict<PgTableConstraint.Unique> _uniqueConstraints = new(nameHandling);
    private readonly DatabaseComponentDict<PreForeignKey> _foreignKeys = new(nameHandling);
    private readonly DatabaseComponentDict<PgTableConstraint.Check> _checks = new(nameHandling);
    private readonly DatabaseComponentDict<ExclusionDeclaration> _exclusions = new(nameHandling);
    private readonly DatabaseComponentDict<IndexDeclaration> _indexes = new(nameHandling);
    private readonly HashSet<string> _constraintNames = [];

    public IReadOnlyList<PreColumn> Columns => _columns;
    public PgPartitioning? PartitionBy { get; private set; }
    public PrePartition? PartitionOf { get; private set; }

    public override void ApplyCreateTable(CreateTable createTable)
    {
        if (createTable.Temporary != null)
        {
            throw new DefinitionException.TempTable(createTable.Name.ToString(), SourceRef);
        }
        if (createTable.AsSelect != null)
        {
            throw new DefinitionException.CreateTableAsSelect(createTable.Name.ToString(), SourceRef);
        }
        if (createTable.PgOptions is { OnCommit: not null })
        {
            throw new SqlSyntaxException.OnCommitNonTemporary(createTable.Name.ToString(), SourceRef);
        }
        _unlogged = createTable.PgOptions?.Unlogged == true;
        PartitionBy = createTable.PgOptions?.PartitionBy is { } partitionBy ? _partitionNormalizer.Normalize(partitionBy) : null;
        _accessMethod = ComponentNormalizer.NormalizeSingleIdentifierNullable(createTable.PgOptions?.AccessMethod
            ?? (PartitionBy == null ? serverDefaults.TableAccessMethod : null));
        _tablespace = ComponentNormalizer.NormalizeSingleIdentifierNullable(createTable.PgOptions?.Tablespace
            ?? serverDefaults.Tablespace ?? (PartitionBy == null ? serverDefaults.CatalogTablespace : null));
        if (createTable.PgOptions?.PartitionOf is { } partitionOf)
        {
            foreach (StatementTableOption option in createTable.TableOptions)
            {
                AddOption(option);
            }
            PartitionOf = new PrePartition(ComponentNormalizer.NormalizeObjectName(partitionOf.Parent),
                _partitionNormalizer.Normalize(partitionOf.Bound, SourceRef), createTable, SourceRef);
            return;
        }
        base.ApplyCreateTable(createTable);
    }

    public override void AddColumn(StatementColumn column, SourceRef? sourceRef)
    {
        sourceRef ??= SourceRef;
        if (column is not StatementColumn.Typed typedColumn)
        {
            throw new NotImplementedException("PostgreSQL column construction currently supports typed declarations only.");
        }
        if (!ColumnNames.Add(Handle.GetNamePart(typedColumn.Name, NameHandling)))
        {
            throw new SqlSyntaxException.DuplicateColumn(typedColumn.Name.Name, Name, sourceRef);
        }
        Identifier normalizedName = ComponentNormalizer.NormalizeSingleIdentifier(typedColumn.Name);
        var columnName = new ColumnIdentifier(normalizedName.Name, Name, normalizedName.QuoteStyle)
        {
            Source = normalizedName.Source,
            OriginalIdentifiers = normalizedName.OriginalIdentifiers
        };

        ColumnOption.Nullability? nullability = null;
        PgConstraintOptions nullabilityOptions = new();
        ColumnOption.ColumnDefault? defaultValue = null;
        ColumnOption.Generated? generated = null;
        StatementColumnOption.Generated.PgIdentity? identity = null;
        ObjectName? collation = null;
        PgColumnStorageMode? storage = null;
        PgCompressionMethod compression = PgCompressionMethod.Default;

        var seenOptions = new DuplicateGuard();

        foreach (StatementColumnOption option in typedColumn.Options)
        {
            if (option is StatementColumnOption.Default { PgOptions: not null }
                or StatementColumnOption.PgReference)
            {
                throw new DefinitionException($"{nameof(CreateTable)} PostgreSQL column constraint options", sourceRef);
            }

            if (!seenOptions.Add(option.DuplicateCheckKey))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(option.DuplicateCheckKey, "column", typedColumn.Name, sourceRef);
            }

            switch (option)
            {
                case StatementColumnOption.PrimaryKey primaryKey:
                    AddColumnPrimaryKey(typedColumn.Name, primaryKey, sourceRef);
                    break;
                case StatementColumnOption.Unique unique:
                    AddColumnUnique(typedColumn.Name, unique, sourceRef);
                    break;
                case StatementColumnOption.CheckConstraint check:
                    AddColumnCheck(check, sourceRef);
                    break;
                case StatementColumnOption.Nullability nullabilityOption:
                    nullabilityOptions = PgConstraintNormalizer.Normalize(nullabilityOption.PgOptions, "NOT NULL",
                        columnName, sourceRef, allowNoInherit: nullabilityOption is StatementColumnOption.Nullability.NotNull);
                    Identifier? nullabilityName = ComponentNormalizer.NormalizeSingleIdentifierNullable(nullabilityOption.Name);
                    nullability = nullabilityOption is StatementColumnOption.Nullability.NotNull
                        ? new ColumnOption.Nullability.NotNull(nullabilityName)
                        : new ColumnOption.Nullability.Null(nullabilityName);
                    break;
                case StatementColumnOption.Default defaultOption:
                    defaultValue = NormalizeDefault(defaultOption);
                    break;
                case StatementColumnOption.Generated.AsExpression expression:
                    Expression normalized = GetNormalizedExpression(expression.Expression);
                    ValidateExpressionScope(normalized, sourceRef);
                    generated = new ColumnOption.Generated.AsExpression(normalized,
                        expression.ModeGiven ? expression.Mode : attributeDefaults.GeneratedColumnMode);
                    break;
                case StatementColumnOption.Generated.PgIdentity identityOption:
                    identity = identityOption;
                    break;
                case StatementColumnOption.PgCollation collationOption:
                    collation = ComponentNormalizer.NormalizeObjectName(collationOption.Name);
                    break;
                case StatementColumnOption.PgStorage storageOption:
                    storage = storageOption.Mode;
                    break;
                case StatementColumnOption.PgCompression compressionOption:
                    compression = compressionOption.Method;
                    break;
                default:
                    throw new NotImplementedException($"PostgreSQL column option {option.GetType().Name} is not implemented.");
            }
        }
        if (generated != null && defaultValue != null)
        {
            throw new SqlSyntaxException.MutuallyExclusive("GENERATED", "DEFAULT", "column", typedColumn.Name.Name, sourceRef);
        }
        if (identity != null && defaultValue != null)
        {
            throw new SqlSyntaxException.MutuallyExclusive("IDENTITY", "DEFAULT", "column", typedColumn.Name.Name, sourceRef);
        }
        if (identity != null)
        {
            if (nullability is ColumnOption.Nullability.Null)
            {
                throw new SqlSyntaxException.MutuallyExclusive("IDENTITY", "NULL", "column", typedColumn.Name.Name, sourceRef);
            }
            nullability ??= new ColumnOption.Nullability.NotNull();
        }
        _columns.Add(ApplyPrimaryKeyNullability(new PreColumn(columnName, NormalizeDataTypeReference(typedColumn.DataType), nullability,
            defaultValue, nullability is ColumnOption.Nullability.NotNull)
        {
            Generated = generated,
            Identity = identity,
            Collation = collation,
            Storage = storage,
            Compression = compression,
            NullabilityOptions = nullabilityOptions,
            SourceRef = sourceRef
        }));
    }

    private static DataTypeReference NormalizeDataTypeReference(DataTypeReference reference)
    {
        if (reference is DataTypeReference.Array array)
        {
            DataTypeReference element = NormalizeDataTypeReference(array.ElementType);
            return element is DataTypeReference.BuiltIn builtIn
                ? new DataTypeReference.BuiltIn(new PgDataType.Array(builtIn.DataType))
                : array with { ElementType = element };
        }
        return reference;
    }

    public override void AddIndexOrConstraint(StatementTableConstraint constraint, SourceRef? sourceRef)
    {
        sourceRef ??= SourceRef;
        if (constraint is StatementTableConstraint.PgUsingIndex)
        {
            throw new DefinitionException($"{nameof(CreateTable)} PostgreSQL constraint options", sourceRef);
        }
        switch (constraint)
        {
            case StatementTableConstraint.PrimaryKey primaryKey:
                AddTablePrimaryKey(primaryKey, sourceRef);
                break;
            case StatementTableConstraint.UniqueConstraint unique:
                AddTableUnique(unique, sourceRef);
                break;
            case StatementTableConstraint.ForeignKey foreignKey:
                AddTableForeignKey(foreignKey, sourceRef);
                break;
            case StatementTableConstraint.Check check:
                AddTableCheck(check, sourceRef);
                break;
            case StatementTableConstraint.PgExclusion exclusion:
                AddTableExclusion(exclusion, sourceRef);
                break;
            default:
                throw new NotImplementedException($"PostgreSQL constraint {constraint.GetType().Name} is not implemented.");
        }
    }

    private void AddColumnPrimaryKey(Identifier column, StatementColumnOption.PrimaryKey primaryKey, SourceRef? sourceRef)
    {
        AddTablePrimaryKey(new StatementTableConstraint.PrimaryKey([new StatementKeyPart.Column(column)], primaryKey.Name)
        {
            PgOptions = primaryKey.PgOptions,
            IndexParameters = primaryKey.IndexParameters
        }, sourceRef);
    }

    private void AddColumnUnique(Identifier column, StatementColumnOption.Unique unique, SourceRef? sourceRef)
    {
        AddTableUnique(new StatementTableConstraint.UniqueConstraint([new StatementKeyPart.Column(column)], unique.Name)
        {
            NullsDistinct = unique.NullsDistinct,
            IndexParameters = unique.IndexParameters,
            PgOptions = unique.PgOptions
        }, sourceRef);
    }

    private void AddColumnCheck(StatementColumnOption.CheckConstraint check, SourceRef? sourceRef)
    {
        AddTableCheck(new StatementTableConstraint.Check(check.Expression, check.Name)
        {
            PgOptions = check.PgOptions
        }, sourceRef);
    }

    private void AddTablePrimaryKey(StatementTableConstraint.PrimaryKey primaryKey, SourceRef? sourceRef)
    {
        ValidateTablePrimaryKey(primaryKey, sourceRef);
        if (_primaryKey != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("PRIMARY KEY", "table", Name, sourceRef);
        }
        Identifier keyName = AddConstraintName(primaryKey.Name, "PRIMARY KEY", sourceRef);
        if (_indexes.ContainsKey(keyName))
        {
            throw new SqlSyntaxException.DuplicateObject(keyName, sourceRef);
        }
        SqlValueList<Identifier> columns = NormalizeConstraintColumns(primaryKey.Columns, keyName, sourceRef);
        _primaryKey = new PgPrimaryKey(columns, keyName)
        {
            IncludedColumns = NormalizeIncludedColumns(primaryKey.IncludedColumns, columns, keyName, sourceRef),
            Options = PgConstraintNormalizer.Normalize(primaryKey.PgOptions, "PRIMARY KEY", keyName, sourceRef, allowDeferrable: true),
            BackingIndex = CreateBackingIndex(columns, keyName, primaryKey.IncludedColumns, primaryKey.IndexParameters?.Tablespace)
        };
        for (int index = 0; index < _columns.Count; index++)
        {
            _columns[index] = ApplyPrimaryKeyNullability(_columns[index]);
        }
    }

    private PreColumn ApplyPrimaryKeyNullability(PreColumn column)
    {
        if (_primaryKey == null || !_primaryKey.Columns.Any(keyColumn => keyColumn.Name == column.Name.Name))
        {
            return column;
        }
        return column with
        {
            NotNull = true,
            Nullability = new ColumnOption.Nullability.NotNull(
                column.Nullability is ColumnOption.Nullability.NotNull notNull ? notNull.Name : null)
        };
    }

    private void AddTableUnique(StatementTableConstraint.UniqueConstraint unique, SourceRef? sourceRef)
    {
        ValidateTableUnique(unique, sourceRef);
        Identifier normalizedName = AddConstraintName(unique.Name, "UNIQUE constraint", sourceRef);
        if (_indexes.ContainsKey(normalizedName))
        {
            throw new SqlSyntaxException.DuplicateObject(normalizedName, sourceRef);
        }
        SqlValueList<Identifier> columns = NormalizeConstraintColumns(unique.Columns, normalizedName, sourceRef);
        _uniqueConstraints.Add(normalizedName, new PgTableConstraint.Unique(columns, normalizedName)
        {
            IncludedColumns = NormalizeIncludedColumns(unique.IncludedColumns, columns, normalizedName, sourceRef),
            NullsDistinct = unique.NullsDistinct ?? true,
            Options = PgConstraintNormalizer.Normalize(unique.PgOptions, "UNIQUE", normalizedName, sourceRef, allowDeferrable: true),
            BackingIndex = CreateBackingIndex(columns, normalizedName, unique.IncludedColumns, unique.IndexParameters?.Tablespace)
        });
    }

    private void AddTableForeignKey(StatementTableConstraint.ForeignKey foreignKey, SourceRef? sourceRef)
    {
        ValidateConstraintIndexOptions(foreignKey.Comment, sourceRef);
        Identifier constraintName = AddConstraintName(foreignKey.Name, "FOREIGN KEY", sourceRef);
        _foreignKeys.Add(constraintName, new PreForeignKey(constraintName,
            NormalizeConstraintColumns([.. foreignKey.Columns.Select(column => new StatementKeyPart.Column(column))], constraintName, sourceRef),
            foreignKey.ForeignTable, [.. foreignKey.ForeignColumns.Select(ComponentNormalizer.NormalizeSingleIdentifier)],
            foreignKey.OnDelete ?? attributeDefaults.ForeignKeyOnDelete,
            foreignKey.OnUpdate ?? attributeDefaults.ForeignKeyOnUpdate,
            PgConstraintNormalizer.Normalize(foreignKey.PgOptions, "FOREIGN KEY", constraintName, sourceRef, allowDeferrable: true),
            sourceRef));
    }

    private void AddTableCheck(StatementTableConstraint.Check check, SourceRef? sourceRef)
    {
        ValidateConstraintIndexOptions(check.Comment, sourceRef);
        Identifier constraintName = AddConstraintName(check.Name, "CHECK", sourceRef);
        Expression expression = GetNormalizedExpression(check.Expression);
        ValidateExpressionScope(expression, sourceRef);
        _checks.Add(constraintName, new PgTableConstraint.Check(expression, constraintName)
        {
            Options = PgConstraintNormalizer.Normalize(check.PgOptions, "CHECK", constraintName, sourceRef, allowNoInherit: true)
        });
    }

    private void AddTableExclusion(StatementTableConstraint.PgExclusion exclusion, SourceRef? sourceRef)
    {
        ValidateConstraintIndexOptions(exclusion.Comment, sourceRef);
        Identifier name = AddConstraintName(exclusion.Name, "EXCLUDE", sourceRef);
        if (_indexes.ContainsKey(name))
        {
            throw new SqlSyntaxException.DuplicateObject(name, sourceRef);
        }
        IndexMethod method = exclusion.Method ?? new IndexMethod.BuiltIn(attributeDefaults.IndexMethod);
        if (method is IndexMethod.Custom custom)
        {
            method = custom with { Method = ComponentNormalizer.NormalizeSingleIdentifier(custom.Method) };
        }
        ValidateIncludedColumnMethod(method, exclusion.IncludedColumns, sourceRef);
        PgTableConstraint.Exclusion normalized = _exclusionNormalizer.Normalize(exclusion, name, method,
            GetIndexTablespace(exclusion.IndexParameters?.Tablespace), sourceRef);
        normalized = normalized with
        {
            BackingIndex = normalized.BackingIndex with
            {
                IncludedColumns = NormalizeIncludedColumns(exclusion.IncludedColumns,
                    normalized.BackingIndex.Columns.OfType<PgKeyPart.Column>().Select(column => column.Name), name, sourceRef)
            }
        };
        foreach (PgKeyPart.IndexExpression key in normalized.BackingIndex.Columns.OfType<PgKeyPart.IndexExpression>())
        {
            ValidateExpressionScope(key.Expression, sourceRef);
        }
        if (normalized.BackingIndex.Predicate != null)
        {
            ValidateExpressionScope(normalized.BackingIndex.Predicate, sourceRef);
        }
        _exclusions.Add(name, new ExclusionDeclaration(normalized, sourceRef));
    }

    private static void ValidateTablePrimaryKey(StatementTableConstraint.PrimaryKey primaryKey, SourceRef? sourceRef)
    {
        if (primaryKey.IndexParameters?.StorageParameters != null)
        {
            throw new DefinitionException($"{nameof(CreateTable)} PostgreSQL primary key index parameters", sourceRef);
        }
        ValidateConstraintIndexOptions(primaryKey.Comment, sourceRef, primaryKey.IndexMethod != null ? new IndexMethod.BuiltIn(primaryKey.IndexMethod) : null,
            primaryKey.IndexOrganization, primaryKey.Options, primaryKey.StorageLocation);
    }

    private static void ValidateTableUnique(StatementTableConstraint.UniqueConstraint unique, SourceRef? sourceRef)
    {
        if (unique.IndexParameters?.StorageParameters != null)
        {
            throw new DefinitionException($"{nameof(CreateTable)} PostgreSQL unique constraint options", sourceRef);
        }
        ValidateConstraintIndexOptions(unique.Comment, sourceRef, unique.IndexMethod,
            unique.IndexOrganization, unique.Options, unique.StorageLocation);
        if (unique.IndexName != null)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "separate UNIQUE index name", sourceRef);
        }
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static void ValidateConstraintIndexOptions(Comment? comment, SourceRef? sourceRef,
        IndexMethod? method = null, IndexOrganization? organization = null,
        SqlValueList<StatementIndexOption>? options = null, IndexStorageLocation? storageLocation = null)
    {
        if (comment != null)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "inline COMMENT", sourceRef);
        }
        if (method != null && method is IndexMethod.BuiltIn builtIn && builtIn.Method != BuiltInIndexMethod.Btree)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"USING {builtIn.Method} on PRIMARY KEY or UNIQUE", sourceRef);
        }
        if (organization != null)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", organization.ToString(), sourceRef);
        }
        ValidateIndexStorageParameters(options, sourceRef);
        if (storageLocation != null)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", storageLocation.ToSql(), sourceRef);
        }
    }

    private static void ValidateIndexStorageParameters(SqlValueList<StatementIndexOption>? options, SourceRef? sourceRef)
    {
        if (options == null)
        {
            return;
        }
        foreach (StatementIndexOption option in options)
        {
            if (option is StatementIndexOption.FillFactor)
            {
                throw new NotImplementedException("PostgreSQL FILLFACTOR support is not implemented.");
            }
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", option.ToSql(), sourceRef);
        }
    }

    public void AddIndex(CreateIndex statement, SourceRef? sourceRef)
    {
        sourceRef ??= SourceRef;
        if (statement.Name == null)
        {
            throw new DefinitionException.TableConstraintNameRequired("INDEX", Name, sourceRef);
        }
        Identifier name = statement.Name;
        switch (statement)
        {
            case CreateIndex.Standard standard:
                ValidateConstraintIndexOptions(standard.Comment, sourceRef, organization: standard.IndexOrganization,
                    options: standard.Options, storageLocation: standard.StorageLocation);
                AddIndexDeclaration(name, standard.Columns, unique: false, standard.IndexMethod, standard.Filter,
                    standard.IncludedColumns, standard.PgOptions?.Tablespace, sourceRef);
                break;
            case CreateIndex.Unique unique:
                ValidateConstraintIndexOptions(unique.Comment, sourceRef, organization: unique.IndexOrganization,
                    options: unique.Options, storageLocation: unique.StorageLocation);
                AddIndexDeclaration(name, unique.Columns, unique: true, unique.IndexMethod, unique.Filter,
                    unique.IncludedColumns, unique.PgOptions?.Tablespace, sourceRef);
                break;
            default:
                throw new NotSupportedException("Only basic PostgreSQL standard and unique indexes are supported.");
        }
    }

    private Identifier AddConstraintName(Identifier? name, string constraintType, SourceRef? sourceRef)
    {
        if (name == null)
        {
            throw new DefinitionException.TableConstraintNameRequired(constraintType, Name, sourceRef);
        }
        if (!_constraintNames.Add(Handle.GetNamePart(name, NameHandling)))
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce(name.Name, "table", Name, sourceRef);
        }
        return ComponentNormalizer.NormalizeSingleIdentifier(name);
    }

    private void AddIndexDeclaration(Identifier name, SqlValueList<StatementKeyPart> columns, bool unique, IndexMethod? method,
        Expression? predicate, IncludedColumns? includedColumns = null, Identifier? tablespace = null, SourceRef? sourceRef = null)
    {
        method ??= new IndexMethod.BuiltIn(attributeDefaults.IndexMethod);
        name = ComponentNormalizer.NormalizeSingleIdentifier(name);
        if (_indexes.ContainsKey(name) || _uniqueConstraints.ContainsKey(name) || _exclusions.ContainsKey(name))
        {
            throw new SqlSyntaxException.DuplicateObject(name, sourceRef);
        }
        if (_primaryKey?.Name != null && _primaryKey.Name.Name == name.Name)
        {
            throw new SqlSyntaxException.DuplicateObject(name, sourceRef);
        }
        if (!IndexMethod.IsBtree(method) && columns.Any(part => part switch
            {
                StatementKeyPart.Column column => column.Direction != null,
                StatementKeyPart.IndexExpression expression => expression.Direction != null,
                _ => false
            }))
        {
            throw new SqlSyntaxException.IndexDirectionNotSupported(name.Name, method.ToSql(), Name.ToString(), sourceRef);
        }
        if (!IndexMethod.IsBtree(method) && unique)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"UNIQUE USING {method.ToSql()}", sourceRef);
        }
        ValidateIncludedColumnMethod(method, includedColumns, sourceRef);
        SqlValueList<PgKeyPart> parts = NormalizeIndexParts(columns, sourceRef);
        foreach (PgKeyPart.IndexExpression part in parts.OfType<PgKeyPart.IndexExpression>())
        {
            ValidateExpressionScope(part.Expression, sourceRef);
        }
        Expression? normalizedPredicate = predicate != null ? GetNormalizedExpression(predicate) : null;
        if (normalizedPredicate != null)
        {
            ValidateExpressionScope(normalizedPredicate, sourceRef);
        }
        _indexes.Add(name, new IndexDeclaration(new PgIndex(parts)
        {
            Name = name,
            Unique = unique,
            Method = method,
            Tablespace = GetIndexTablespace(tablespace),
            Predicate = normalizedPredicate,
            IncludedColumns = NormalizeIncludedColumns(includedColumns,
                parts.OfType<PgKeyPart.Column>().Select(column => column.Name), name, sourceRef)
        }, sourceRef));
    }

    private static void ValidateIncludedColumnMethod(IndexMethod method, IncludedColumns? includedColumns, SourceRef? sourceRef)
    {
        if (includedColumns != null && !IndexMethod.IsBtree(method) && method is IndexMethod.BuiltIn builtIn && builtIn.Method != BuiltInIndexMethod.Gist && builtIn.Method != BuiltInIndexMethod.SpGist)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"INCLUDE with {method.ToSql()} indexes", sourceRef);
        }
    }

    public override void AddOption(StatementTableOption tableOption)
    {
        throw new NotImplementedException();
    }

    public override PseudoTable ToPseudoTable()
    {
        return new PseudoTable(Name.Name, Name, [.. _columns.Select(column => column.Name.Name)], PseudoTableType.Table);
    }

    public override PgTable ToTable()
    {
        throw new NotImplementedException("PostgreSQL table construction requires a path and type resolution manager.");
    }


    private PgIdentityOptions ResolveIdentityOptions(StatementColumnOption.Generated.PgIdentity identity,
        DataType dataType, ColumnIdentifier column, SourceRef? sourceRef)
    {
        (long typeMinimum, long typeMaximum) = dataType switch
        {
            PgDataType.PgSmallInt => (short.MinValue, short.MaxValue),
            DataType.Int => (int.MinValue, int.MaxValue),
            DataType.BigInt => (long.MinValue, long.MaxValue),
            _ => throw new SqlSyntaxException.AutoIncrementIntegerOnly(column.ToSimpleIdentifier(), Name, sourceRef)
        };
        long? start = null;
        long increment = 1;
        long? minimum = null;
        long? maximum = null;
        long cache = 1;
        bool cycle = false;
        var seenOptions = new HashSet<string>(StringComparer.Ordinal);
        foreach (PgStatementSequenceOption option in identity.Options ?? [])
        {
            if (!seenOptions.Add(option.DuplicateCheckKey))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(option.DuplicateCheckKey, "column", column.ToSimpleIdentifier(), sourceRef);
            }
            switch (option)
            {
                case PgStatementSequenceOption.Start startOption:
                    start = startOption.Value;
                    break;
                case PgStatementSequenceOption.Increment incrementOption:
                    increment = incrementOption.Value;
                    break;
                case PgStatementSequenceOption.MinValue minimumOption:
                    minimum = minimumOption.Value;
                    break;
                case PgStatementSequenceOption.MaxValue maximumOption:
                    maximum = maximumOption.Value;
                    break;
                case PgStatementSequenceOption.Cache cacheOption:
                    cache = cacheOption.Value;
                    break;
                case PgStatementSequenceOption.Cycle cycleOption:
                    cycle = cycleOption.Enabled;
                    break;
                case PgStatementSequenceOption.SequenceName:
                    break;
                default:
                    throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"identity sequence option {option.DuplicateCheckKey}", sourceRef);
            }
        }
        if (increment == 0)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "zero identity sequence increment", sourceRef);
        }
        if (cache <= 0)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "nonpositive identity sequence cache", sourceRef);
        }
        long effectiveMinimum = minimum ?? (increment > 0 ? 1 : typeMinimum);
        long effectiveMaximum = maximum ?? (increment > 0 ? typeMaximum : -1);
        if (effectiveMinimum < typeMinimum || effectiveMinimum > typeMaximum
            || effectiveMaximum < typeMinimum || effectiveMaximum > typeMaximum)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "identity sequence bounds outside the integer type range", sourceRef);
        }
        if (effectiveMinimum >= effectiveMaximum)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "identity sequence MINVALUE not less than MAXVALUE", sourceRef);
        }
        long effectiveStart = start ?? (increment > 0 ? effectiveMinimum : effectiveMaximum);
        if (effectiveStart < effectiveMinimum || effectiveStart > effectiveMaximum)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "identity sequence start outside its bounds", sourceRef);
        }
        return new PgIdentityOptions(effectiveStart, increment, effectiveMinimum, effectiveMaximum, cache, cycle);
    }

    public PgTable ToTable(PgTypeResolutionManager manager)
    {
        if (PartitionOf != null)
        {
            throw new DefinitionException($"{nameof(CreateTable)} PostgreSQL partition parent resolution", PartitionOf.SourceRef);
        }
        SqlValueList<PgColumn> columns = [.. _columns.Select(column =>
        {
            DataType dataType = manager.Resolve(column.DataType, Name.Schema, column.SourceRef);
            PgColumnStorageMode? typeStorage = PgColumnDefaults.GetStorage(dataType);
            PgIdentityOptions? identityOptions = column.Identity is { } identity
                ? ResolveIdentityOptions(identity, dataType, column.Name, column.SourceRef)
                : null;
            ObjectName? sequenceName = column.Identity?.Options?.OfType<PgStatementSequenceOption.SequenceName>().SingleOrDefault()?.Name;
            ObjectName? typeCollation = PgColumnDefaults.GetCollation(dataType, column.Collation);
            return new PgColumn(column.Name, dataType)
            {
                Collation = ComponentNormalizer.NormalizeObjectNameNullable(typeCollation),
                Nullability = column.Nullability ?? (typeStorage != null ? new ColumnOption.Nullability.Null() : null),
                NullabilityOptions = column.NullabilityOptions,
                Default = typeStorage != null && column.Default is ColumnOption.ColumnDefault.DefaultValue { Value: Value.Null }
                    ? null : column.Default,
                Storage = column.Storage == null || column.Storage == PgColumnStorageMode.Default ? typeStorage : column.Storage,
                Compression = column.Compression,
                Generated = identityOptions != null
                    ? new ColumnOption.Generated.PgIdentity(column.Identity!.Always, identityOptions)
                    : column.Generated,
                BackingSequence = identityOptions == null ? null : new PgBackingSequence(
                    sequenceName == null
                        ? ComponentNormalizer.NormalizeObjectIdentifier(PgBackingObjectNames.GetIdentitySequenceName(column.Name))
                        : ComponentNormalizer.NormalizeObjectIdentifier(ObjectIdentifier.FromObjectName(sequenceName, Name.Schema, null)),
                    dataType, identityOptions)
                {
                    NameExplicit = sequenceName != null,
                    Unlogged = _unlogged
                }
            };
        })];

        if (_primaryKey != null)
        {
            ValidateColumnReferences(_primaryKey.Columns, _primaryKey.Name.Name, SourceRef);
            ValidateColumnReferences(_primaryKey.IncludedColumns?.Columns ?? [], _primaryKey.Name.Name, SourceRef);
        }

        foreach (PgTableConstraint.Unique unique in _uniqueConstraints.Values)
        {
            ValidateColumnReferences(unique.Columns, unique.Name!.Name, SourceRef);
            ValidateColumnReferences(unique.IncludedColumns?.Columns ?? [], unique.Name.Name, SourceRef);
        }

        var foreignKeys = new DatabaseComponentDict<PgTableConstraint.ForeignKey>(NameHandling);
        foreach (PreForeignKey foreignKey in _foreignKeys.Values)
        {
            ValidateColumnReferences(foreignKey.Columns, foreignKey.Name.Name, foreignKey.SourceRef);
            foreignKeys.Add(foreignKey.Name, new PgTableConstraint.ForeignKey(foreignKey.Columns,
                ResolveForeignTable(foreignKey, manager), foreignKey.ReferencedColumns, foreignKey.Name)
            {
                OnDelete = foreignKey.OnDelete,
                OnUpdate = foreignKey.OnUpdate,
                Options = foreignKey.Options
            });
        }

        var indexes = new DatabaseComponentDict<PgIndex>(NameHandling);
        foreach (IndexDeclaration declaration in _indexes.Values)
        {
            ValidateIndexColumnReferences(declaration.Index, declaration.SourceRef);
            indexes.Add(declaration.Index.Name, declaration.Index);
        }
        var exclusions = new DatabaseComponentDict<PgTableConstraint.Exclusion>(NameHandling);
        foreach (ExclusionDeclaration declaration in _exclusions.Values)
        {
            ValidateIndexColumnReferences(declaration.Constraint.BackingIndex, declaration.SourceRef);
            exclusions.Add(declaration.Constraint.Name!, declaration.Constraint);
        }

        var result = new PgTable(Name, columns)
        {
            Unlogged = _unlogged,
            AccessMethod = _accessMethod,
            Tablespace = _tablespace,
            PartitionBy = PartitionBy,
            PrimaryKey = _primaryKey,
            Checks = new DatabaseComponentDict<PgTableConstraint.Check>(_checks),
            ForeignKeys = foreignKeys,
            UniqueConstraints = new DatabaseComponentDict<PgTableConstraint.Unique>(_uniqueConstraints),
            Exclusions = exclusions,
            Indexes = indexes
        };

        foreach (PgTableConstraint.Check check in _checks.Values)
        {
            ValidateExpressionColumns(check.Expression, result, NameHandling, SourceRef);
        }
        for (int index = 0; index < result.Columns.Count; index++)
        {
            if (result.Columns[index].Generated is ColumnOption.Generated.AsExpression expression)
            {
                ValidateExpressionColumns(expression.Expression, result, NameHandling, _columns[index].SourceRef);
            }
        }
        foreach (IndexDeclaration declaration in _indexes.Values)
        {
            ValidateIndexExpressions(declaration.Index, result, declaration.SourceRef);
        }
        foreach (ExclusionDeclaration declaration in _exclusions.Values)
        {
            ValidateIndexExpressions(declaration.Constraint.BackingIndex, result, declaration.SourceRef);
        }
        return result;
    }

    private void ValidateIndexColumnReferences(PgIndex index, SourceRef? sourceRef)
    {
        ValidateColumnReferences(index.Columns.OfType<PgKeyPart.Column>().Select(column => column.Name), index.Name.Name, sourceRef);
        ValidateColumnReferences(index.IncludedColumns?.Columns ?? [], index.Name.Name, sourceRef);
    }

    private void ValidateIndexExpressions(PgIndex index, PgTable table, SourceRef? sourceRef)
    {
        foreach (PgKeyPart.IndexExpression part in index.Columns.OfType<PgKeyPart.IndexExpression>())
        {
            ValidateExpressionColumns(part.Expression, table, NameHandling, sourceRef);
        }
        if (index.Predicate != null)
        {
            ValidateExpressionColumns(index.Predicate, table, NameHandling, sourceRef);
        }
    }

    private void ValidateColumnReferences(IEnumerable<Identifier> columns, string keyName, SourceRef? sourceRef)
    {
        foreach (Identifier column in columns)
        {
            if (!ColumnNames.Contains(column.Name))
            {
                throw new SqlSyntaxException.IndexColumnNotFound(keyName, Name, column, sourceRef);
            }
        }
    }

    private Identifier GetIndexTablespace(Identifier? explicitTablespace)
    {
        return ComponentNormalizer.NormalizeSingleIdentifier(explicitTablespace
            ?? serverDefaults.Tablespace ?? serverDefaults.CatalogTablespace);
    }

    private PgIndex CreateBackingIndex(SqlValueList<Identifier> columns, Identifier name, IncludedColumns? includedColumns,
        Identifier? tablespace)
    {
        return new PgIndex(columns.Map<PgKeyPart>(column => new PgKeyPart.Column(column)))
        {
            Name = name,
            NameExplicit = true,
            Unique = true,
            Method = new IndexMethod.BuiltIn(BuiltInIndexMethod.Btree),
            Tablespace = GetIndexTablespace(tablespace),
            IncludedColumns = includedColumns == null ? null : new IncludedColumns(includedColumns.Columns.Map(ComponentNormalizer.NormalizeSingleIdentifier))
        };
    }

    private IncludedColumns? NormalizeIncludedColumns(IncludedColumns? includedColumns, IEnumerable<Identifier> keyColumns,
        Identifier indexName, SourceRef? indexSource)
    {
        if (includedColumns == null)
        {
            return null;
        }
        if (includedColumns.Columns.Count == 0)
        {
            throw new SqlSyntaxException.EmptyIndexColumnList("An index INCLUDE clause", indexSource);
        }
        var seen = keyColumns.Select(column => column.Name).ToHashSet();
        SqlValueList<Identifier> normalized = [];
        foreach (Identifier column in includedColumns.Columns)
        {
            Identifier actual = ComponentNormalizer.NormalizeSingleIdentifier(column);
            if (!seen.Add(actual.Name))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(actual.Name, "index", indexName, indexSource);
            }
            normalized.Add(actual);
        }
        return new IncludedColumns(normalized);
    }

    private ObjectIdentifier ResolveForeignTable(PreForeignKey foreignKey, PgTypeResolutionManager manager)
    {
        try
        {
            return ((PgDataType.Custom)manager.Resolve(new DataTypeReference.Named(foreignKey.ReferencedTable), Name.Schema, foreignKey.SourceRef)).Name;
        }
        catch (SqlSyntaxException.TypeNotFound)
        {
            throw new SqlSyntaxException.ForeignKeyReferencedTableNotFound(foreignKey.Name.Name, Name,
                ObjectIdentifier.FromObjectName(foreignKey.ReferencedTable, Name.Schema, null), foreignKey.SourceRef);
        }
    }

    private SqlValueList<Identifier> NormalizeConstraintColumns(SqlValueList<StatementKeyPart> keyParts, Identifier? constraintName, SourceRef? sourceRef)
    {
        if (keyParts.Count == 0)
        {
            throw new SqlSyntaxException.EmptyIndexColumnList("A primary key", sourceRef);
        }
        SqlValueList<Identifier> result = [];
        var seen = new HashSet<string>();
        foreach (StatementKeyPart keyPart in keyParts)
        {
            if (keyPart is not StatementKeyPart.Column { Length: null, Direction: null } column)
            {
                throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "index expressions, prefix lengths, or sort directions in PRIMARY KEY/UNIQUE constraints", sourceRef);
            }
            Identifier actual = ComponentNormalizer.NormalizeSingleIdentifier(column.Name);
            if (!seen.Add(actual.Name))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(actual.Name, "constraint", constraintName ?? Name, sourceRef);
            }
            result.Add(actual);
        }
        return result;
    }

    private SqlValueList<PgKeyPart> NormalizeIndexParts(SqlValueList<StatementKeyPart> keyParts, SourceRef? sourceRef)
    {
        if (keyParts.Count == 0)
        {
            throw new SqlSyntaxException.EmptyIndexColumnList("An index", sourceRef);
        }
        SqlValueList<PgKeyPart> result = [];
        foreach (StatementKeyPart part in keyParts)
        {
            if ((part.OperatorClass != null || part.NullsOrder != null))
            {
                throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "standalone index key options", sourceRef);
            }
            switch (part)
            {
                case StatementKeyPart.Column { Length: null } column:
                    result.Add(new PgKeyPart.Column(ComponentNormalizer.NormalizeSingleIdentifier(column.Name))
                    {
                        Direction = column.Direction == Direction.Descending ? Direction.Descending : null
                    });
                    break;
                case StatementKeyPart.IndexExpression expression:
                    result.Add(new PgKeyPart.IndexExpression(GetNormalizedExpression(expression.Expression))
                    {
                        Direction = expression.Direction == Direction.Descending ? Direction.Descending : null
                    });
                    break;
                default:
                    throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "index column prefix lengths", sourceRef);
            }
        }
        return result;
    }

    private static IEnumerable<SqlValueList<Identifier>> GetExpressionColumnReferences(Expression expression)
    {
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        foreach (ItemRef reference in expression.GetReferencedItems(context))
        {
            if (reference is ItemRef.SingleIdentifierRef or ItemRef.CompoundIdentifierRef)
            {
                yield return reference.Identifiers;
            }
        }
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private void ValidateExpressionScope(Expression expression, SourceRef? sourceRef)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        foreach (SqlValueList<Identifier> identifiers in GetExpressionColumnReferences(expression))
        {
            if (identifiers.Count > 1)
            {
                bool ownTable = identifiers.Count <= 4
                    && Handle.GetNamePart(identifiers[^2], NameHandling) == Name.Name
                    && (identifiers.Count < 3 || Handle.GetNamePart(identifiers[^3], NameHandling) == Name.Schema.Name)
                    && (identifiers.Count < 4 || Handle.GetNamePart(identifiers[0], NameHandling) == Name.Schema.Catalog.Name);
                if (!ownTable)
                {
                    throw new NotImplementedException("Composite-field or external-table references in constraint/index expressions are not implemented.");
                }
            }
        }
    }

    internal static void ValidateExpressionColumns(Expression expression, PgTable table, NameHandling nameHandling, SourceRef? sourceRef)
    {
        foreach (SqlValueList<Identifier> identifiers in GetExpressionColumnReferences(expression))
        {
            Identifier column = identifiers[^1];
            string name = Handle.GetNamePart(column, nameHandling);
            if (!table.Columns.Any(item => item.Name.Name == name))
            {
                throw new SqlSyntaxException.SelectIdentifierNotFound(column.Name, sourceRef);
            }
        }
    }

    protected override DataType GetNormalizedDataType(DataType dataType)
    {
        throw new NotImplementedException();
    }

    protected override Expression GetNormalizedExpression(Expression expression)
    {
        return expression;
    }

    protected override (Expression Normalized, Expression Flattened) GetNormalizedExpressions(Expression expression)
    {
        return (expression, expression);
    }

    public sealed record PreColumn(ColumnIdentifier Name, DataTypeReference DataType,
        ColumnOption.Nullability? Nullability, ColumnOption.ColumnDefault? Default, bool NotNull)
    {
        public ObjectName? Collation { get; init; }
        public PgColumnStorageMode? Storage { get; init; }
        public PgCompressionMethod Compression { get; init; } = PgCompressionMethod.Default;
        public PgConstraintOptions NullabilityOptions { get; init; } = new();
        public ColumnOption.Generated? Generated { get; init; }
        public StatementColumnOption.Generated.PgIdentity? Identity { get; init; }
        public SourceRef? SourceRef { get; init; }
    }

    public sealed record PrePartition(ObjectName Parent, PgPartitionBounds Bound, CreateTable Statement, SourceRef? SourceRef);

    private sealed record ExclusionDeclaration(PgTableConstraint.Exclusion Constraint, SourceRef? SourceRef);

    private sealed record PreForeignKey(
        Identifier Name,
        SqlValueList<Identifier> Columns,
        ObjectName ReferencedTable,
        SqlValueList<Identifier> ReferencedColumns,
        ReferentialAction? OnDelete,
        ReferentialAction? OnUpdate,
        PgConstraintOptions Options,
        SourceRef? SourceRef);

    private sealed record IndexDeclaration(PgIndex Index, SourceRef? SourceRef);
}
