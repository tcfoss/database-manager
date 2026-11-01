using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgTableBuilder(ObjectIdentifier name, NameHandling nameHandling, INormalizeComponents componentNormalizer, SourceRef? sourceRef)
    : TableBuilder<PgTable>(name, nameHandling, componentNormalizer, sourceRef)
{
    private readonly List<PreColumn> _columns = [];
    private PgPrimaryKey? _primaryKey;
    private readonly DatabaseComponentDict<PgTableConstraint.Unique> _uniqueConstraints = new(nameHandling);
    private readonly DatabaseComponentDict<PreForeignKey> _foreignKeys = new(nameHandling);
    private readonly DatabaseComponentDict<PgTableConstraint.Check> _checks = new(nameHandling);
    private readonly DatabaseComponentDict<IndexDeclaration> _indexes = new(nameHandling);
    private readonly HashSet<string> _constraintNames = [];

    public IReadOnlyList<PreColumn> Columns => _columns;

    public override void AddColumn(StatementColumn column, SourceRef? sourceRef)
    {
        if (column is not StatementColumn.Typed typedColumn)
        {
            throw new NotImplementedException("PostgreSQL column construction currently supports typed declarations only.");
        }
        if (!ColumnNames.Add(Handle.GetNamePart(typedColumn.Name, NameHandling)))
        {
            throw new SqlSyntaxException.DuplicateColumn(typedColumn.Name.Name, Name, sourceRef ?? SourceRef);
        }
        Identifier normalizedName = ComponentNormalizer.NormalizeSingleIdentifier(typedColumn.Name);
        var columnName = new ColumnIdentifier(normalizedName.Name, Name, normalizedName.QuoteStyle)
        {
            Source = normalizedName.Source,
            OriginalIdentifiers = normalizedName.OriginalIdentifiers
        };
        ColumnOption.Nullability? nullability = null;
        ColumnOption.ColumnDefault? defaultValue = null;
        var seenOptions = new HashSet<string>();
        foreach (StatementColumnOption option in typedColumn.Options)
        {
            if (!seenOptions.Add(option.DuplicateCheckKey))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(option.DuplicateCheckKey, "column", typedColumn.Name, sourceRef ?? SourceRef);
            }
            switch (option)
            {
                case StatementColumnOption.PrimaryKey primaryKey:
                    AddColumnPrimaryKey(typedColumn.Name, primaryKey, sourceRef ?? SourceRef);
                    break;
                case StatementColumnOption.Unique unique:
                    AddColumnUnique(typedColumn.Name, unique, sourceRef ?? SourceRef);
                    break;
                case StatementColumnOption.CheckConstraint check:
                    AddColumnCheck(check, sourceRef ?? SourceRef);
                    break;
                case StatementColumnOption.Nullability nullabilityOption:
                    Identifier? nullabilityName = ComponentNormalizer.NormalizeSingleIdentifierNullable(nullabilityOption.Name);
                    nullability = nullabilityOption is StatementColumnOption.Nullability.NotNull
                        ? new ColumnOption.Nullability.NotNull(nullabilityName)
                        : new ColumnOption.Nullability.Null(nullabilityName);
                    break;
                case StatementColumnOption.Default defaultOption:
                    defaultValue = NormalizeDefault(defaultOption);
                    break;
                default:
                    throw new NotImplementedException($"PostgreSQL column option {option.GetType().Name} is not implemented.");
            }
        }
        _columns.Add(ApplyPrimaryKeyNullability(new PreColumn(columnName, NormalizeDataTypeReference(typedColumn.DataType), nullability,
            defaultValue, nullability is ColumnOption.Nullability.NotNull)));
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
            default:
                throw new NotImplementedException($"PostgreSQL constraint {constraint.GetType().Name} is not implemented.");
        }
    }

    private void AddColumnPrimaryKey(Identifier column, StatementColumnOption.PrimaryKey primaryKey, SourceRef? sourceRef)
    {
        AddTablePrimaryKey(new StatementTableConstraint.PrimaryKey([new KeyPart.Column(column)], primaryKey.Name), sourceRef);
    }

    private void AddColumnUnique(Identifier column, StatementColumnOption.Unique unique, SourceRef? sourceRef)
    {
        AddTableUnique(new StatementTableConstraint.UniqueConstraint([new KeyPart.Column(column)], unique.Name), sourceRef);
    }

    private void AddColumnCheck(StatementColumnOption.CheckConstraint check, SourceRef? sourceRef)
    {
        AddTableCheck(new StatementTableConstraint.Check(check.Expression, check.Name), sourceRef);
    }

    private void AddTablePrimaryKey(StatementTableConstraint.PrimaryKey primaryKey, SourceRef? sourceRef)
    {
        ValidateTablePrimaryKey(primaryKey, sourceRef);
        if (_primaryKey != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("PRIMARY KEY", "table", Name, sourceRef);
        }
        Identifier? keyName = ComponentNormalizer.NormalizeSingleIdentifierNullable(primaryKey.Name);
        if (keyName != null && _indexes.ContainsKey(keyName))
        {
            throw new SqlSyntaxException.DuplicateObject(keyName, sourceRef);
        }
        AddConstraintName(primaryKey.Name, "PRIMARY KEY", sourceRef, required: false);
        _primaryKey = new PgPrimaryKey(NormalizeConstraintColumns(primaryKey.Columns, keyName, sourceRef), keyName);
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
        Identifier constraintName = unique.Name
            ?? throw new DefinitionException.TableConstraintNameRequired("UNIQUE constraint", Name, sourceRef);
        Identifier normalizedName = ComponentNormalizer.NormalizeSingleIdentifier(constraintName);
        if (_indexes.ContainsKey(normalizedName))
        {
            throw new SqlSyntaxException.DuplicateObject(constraintName, sourceRef);
        }
        AddConstraintName(constraintName, "UNIQUE", sourceRef, required: true);
        SqlValueList<Identifier> columns = NormalizeConstraintColumns(unique.Columns, normalizedName, sourceRef);
        _uniqueConstraints.Add(normalizedName, new PgTableConstraint.Unique(columns, normalizedName)
        {
            IncludedColumns = NormalizeIncludedColumns(unique.IncludedColumns, columns, normalizedName, sourceRef)
        });
    }

    private void AddTableForeignKey(StatementTableConstraint.ForeignKey foreignKey, SourceRef? sourceRef)
    {
        ValidateConstraintIndexOptions(foreignKey.Comment, sourceRef);
        AddConstraintName(foreignKey.Name, "FOREIGN KEY", sourceRef, required: true);
        Identifier constraintName = ComponentNormalizer.NormalizeSingleIdentifier(foreignKey.Name!);
        _foreignKeys.Add(constraintName, new PreForeignKey(constraintName,
            NormalizeConstraintColumns([.. foreignKey.Columns.Select(column => new KeyPart.Column(column))], constraintName, sourceRef),
            foreignKey.ForeignTable, [.. foreignKey.ForeignColumns.Select(ComponentNormalizer.NormalizeSingleIdentifier)],
            foreignKey.OnDelete, foreignKey.OnUpdate, sourceRef));
    }

    private void AddTableCheck(StatementTableConstraint.Check check, SourceRef? sourceRef)
    {
        ValidateConstraintIndexOptions(check.Comment, sourceRef);
        AddConstraintName(check.Name, "CHECK", sourceRef, required: true);
        Identifier constraintName = ComponentNormalizer.NormalizeSingleIdentifier(check.Name!);
        Expression expression = GetNormalizedExpression(check.Expression);
        ValidateExpressionScope(expression, sourceRef);
        _checks.Add(constraintName, new PgTableConstraint.Check(expression, constraintName));
    }

    private static void ValidateTablePrimaryKey(StatementTableConstraint.PrimaryKey primaryKey, SourceRef? sourceRef)
    {
        ValidateConstraintIndexOptions(primaryKey.Comment, sourceRef, primaryKey.IndexMethod,
            primaryKey.IndexOrganization, primaryKey.Options, primaryKey.StorageLocation);
    }

    private static void ValidateTableUnique(StatementTableConstraint.UniqueConstraint unique, SourceRef? sourceRef)
    {
        ValidateConstraintIndexOptions(unique.Comment, sourceRef, unique.IndexMethod,
            unique.IndexOrganization, unique.Options, unique.StorageLocation);
        if (unique.IndexName != null)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "separate UNIQUE index name", sourceRef);
        }
    }

    private static void ValidateConstraintIndexOptions(Comment? comment, SourceRef? sourceRef,
        IndexMethod? method = null, IndexOrganization? organization = null,
        SqlValueList<StatementIndexOption>? options = null, IndexStorageLocation? storageLocation = null)
    {
        if (comment != null)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "inline COMMENT", sourceRef);
        }
        if (method != null && method != IndexMethod.Btree)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"USING {method} on PRIMARY KEY or UNIQUE", sourceRef);
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
        switch (statement)
        {
            case CreateIndex.Standard standard:
                ValidateConstraintIndexOptions(standard.Comment, sourceRef, organization: standard.IndexOrganization,
                    options: standard.Options, storageLocation: standard.StorageLocation);
                AddIndexDeclaration(standard.Name, standard.Columns, unique: false, standard.IndexMethod, standard.Filter, sourceRef, standard.IncludedColumns);
                break;
            case CreateIndex.Unique unique:
                ValidateConstraintIndexOptions(unique.Comment, sourceRef, organization: unique.IndexOrganization,
                    options: unique.Options, storageLocation: unique.StorageLocation);
                AddIndexDeclaration(unique.Name, unique.Columns, unique: true, unique.IndexMethod, unique.Filter, sourceRef, unique.IncludedColumns);
                break;
            default:
                throw new NotSupportedException("Only basic PostgreSQL standard and unique indexes are supported.");
        }
    }

    private void AddConstraintName(Identifier? name, string constraintType, SourceRef? sourceRef, bool required)
    {
        if (name == null)
        {
            if (required)
            {
                throw new DefinitionException.TableConstraintNameRequired(constraintType, Name, sourceRef);
            }
            return;
        }
        if (!_constraintNames.Add(Handle.GetNamePart(name, NameHandling)))
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce(name.Name, "table", Name, sourceRef);
        }
    }

    private void AddIndexDeclaration(Identifier? name, SqlValueList<KeyPart> columns, bool unique, IndexMethod? method,
        Expression? predicate, SourceRef? sourceRef, IncludedColumns? includedColumns = null)
    {
        if (name == null)
        {
            throw new DefinitionException.TableConstraintNameRequired(unique ? "UNIQUE constraint" : "INDEX", Name, sourceRef);
        }
        name = ComponentNormalizer.NormalizeSingleIdentifier(name);
        if (_indexes.ContainsKey(name) || _uniqueConstraints.ContainsKey(name))
        {
            throw new SqlSyntaxException.DuplicateObject(name, sourceRef);
        }
        if (_primaryKey?.Name != null && _primaryKey.Name.Name == name.Name)
        {
            throw new SqlSyntaxException.DuplicateObject(name, sourceRef);
        }
        if (method != null && method != IndexMethod.Btree && columns.Any(part => part switch
            {
                KeyPart.Column column => column.Direction != null,
                KeyPart.IndexExpression expression => expression.Direction != null,
                _ => false
            }))
        {
            throw new SqlSyntaxException.IndexDirectionNotSupported(name.Name, method.ToString(), Name.ToString(), sourceRef);
        }
        if (method != null && method != IndexMethod.Btree && unique)
        {
            throw new NotSupportedException("Unique indexes and index ordering are only supported with PostgreSQL B-tree indexes.");
        }
        ValidateIncludedColumnMethod(method ?? IndexMethod.Btree, includedColumns);
        SqlValueList<KeyPart> parts = NormalizeIndexParts(columns);
        foreach (KeyPart.IndexExpression part in parts.OfType<KeyPart.IndexExpression>())
        {
            ValidateExpressionScope(part.Expression, sourceRef);
        }
        Expression? normalizedPredicate = predicate != null ? GetNormalizedExpression(predicate) : null;
        if (normalizedPredicate != null)
        {
            ValidateExpressionScope(normalizedPredicate, sourceRef);
        }
        _indexes.Add(name, new IndexDeclaration(new PgIndex(KeyPart.RemoveOptionalElementsFromKeyParts(parts))
        {
            Name = name,
            Unique = unique,
            Method = method ?? IndexMethod.Btree,
            Predicate = normalizedPredicate,
            IncludedColumns = NormalizeIncludedColumns(includedColumns,
                parts.OfType<KeyPart.Column>().Select(column => column.Name), name, sourceRef)
        }, sourceRef));
    }

    private static void ValidateIncludedColumnMethod(IndexMethod method, IncludedColumns? includedColumns)
    {
        if (includedColumns != null && method != IndexMethod.Btree && method != IndexMethod.Gist && method != IndexMethod.SpGist)
        {
            throw new NotSupportedException($"PostgreSQL {method} indexes do not support INCLUDE columns.");
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


    public PgTable ToTable(PgTypeResolutionManager manager)
    {
        SqlValueList<PgColumn> columns = [.. _columns.Select(column =>
            new PgColumn(column.Name, manager.Resolve(column.DataType, Name.Schema, SourceRef))
            {
                Nullability = column.Nullability,
                Default = column.Default
            })];
        if (_primaryKey != null)
        {
            ValidateColumnReferences(_primaryKey.Columns, _primaryKey.Name?.Name ?? "PRIMARY KEY", SourceRef);
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
                OnUpdate = foreignKey.OnUpdate
            });
        }
        var indexes = new DatabaseComponentDict<PgIndex>(NameHandling);
        foreach (IndexDeclaration declaration in _indexes.Values)
        {
            ValidateColumnReferences(declaration.Index.Columns.OfType<KeyPart.Column>().Select(column => column.Name),
                declaration.Index.Name.Name, declaration.SourceRef);
            ValidateColumnReferences(declaration.Index.IncludedColumns?.Columns ?? [], declaration.Index.Name.Name, declaration.SourceRef);
            indexes.Add(declaration.Index.Name, declaration.Index);
        }
        var result = new PgTable(Name, columns)
        {
            PrimaryKey = _primaryKey,
            Checks = new DatabaseComponentDict<PgTableConstraint.Check>(_checks),
            ForeignKeys = foreignKeys,
            UniqueConstraints = new DatabaseComponentDict<PgTableConstraint.Unique>(_uniqueConstraints),
            Indexes = indexes
        };
        foreach (PgTableConstraint.Check check in _checks.Values)
        {
            ValidateExpressionColumns(check.Expression, result, NameHandling, SourceRef);
        }
        foreach (IndexDeclaration declaration in _indexes.Values)
        {
            foreach (KeyPart.IndexExpression part in declaration.Index.Columns.OfType<KeyPart.IndexExpression>())
            {
                ValidateExpressionColumns(part.Expression, result, NameHandling, declaration.SourceRef);
            }
            if (declaration.Index.Predicate != null)
            {
                ValidateExpressionColumns(declaration.Index.Predicate, result, NameHandling, declaration.SourceRef);
            }
        }
        return result;
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

    private IncludedColumns? NormalizeIncludedColumns(IncludedColumns? includedColumns, IEnumerable<Identifier> keyColumns,
        Identifier indexName, SourceRef? indexSource)
    {
        if (includedColumns == null)
        {
            return null;
        }
        if (includedColumns.Columns.Count == 0)
        {
            throw new NotSupportedException("PostgreSQL INCLUDE must contain at least one column.");
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

    private SqlValueList<Identifier> NormalizeConstraintColumns(SqlValueList<KeyPart> keyParts, Identifier? constraintName, SourceRef? sourceRef)
    {
        if (keyParts.Count == 0)
        {
            throw new NotSupportedException("A PostgreSQL key constraint must contain at least one column.");
        }
        SqlValueList<Identifier> result = [];
        var seen = new HashSet<string>();
        foreach (KeyPart keyPart in keyParts)
        {
            if (keyPart is not KeyPart.Column { Length: null, Direction: null } column)
            {
                throw new NotSupportedException("PostgreSQL primary/unique constraints currently support plain column names only.");
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

    private SqlValueList<KeyPart> NormalizeIndexParts(SqlValueList<KeyPart> keyParts)
    {
        if (keyParts.Count == 0)
        {
            throw new NotSupportedException("A PostgreSQL index must contain at least one key part.");
        }
        SqlValueList<KeyPart> result = [];
        foreach (KeyPart part in keyParts)
        {
            switch (part)
            {
                case KeyPart.Column { Length: null } column:
                    result.Add(new KeyPart.Column(ComponentNormalizer.NormalizeSingleIdentifier(column.Name), null, column.Direction));
                    break;
                case KeyPart.IndexExpression expression:
                    result.Add(new KeyPart.IndexExpression(GetNormalizedExpression(expression.Expression), expression.Direction));
                    break;
                default:
                    throw new NotSupportedException("PostgreSQL indexes do not support column prefix lengths.");
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
        ColumnOption.Nullability? Nullability, ColumnOption.ColumnDefault? Default, bool NotNull);

    private sealed record PreForeignKey(Identifier Name, SqlValueList<Identifier> Columns, ObjectName ReferencedTable,
        SqlValueList<Identifier> ReferencedColumns, ReferentialAction? OnDelete, ReferentialAction? OnUpdate, SourceRef? SourceRef);

    private sealed record IndexDeclaration(PgIndex Index, SourceRef? SourceRef);
}
