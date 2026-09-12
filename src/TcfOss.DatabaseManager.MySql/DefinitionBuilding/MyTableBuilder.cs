using System.Diagnostics;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;
using TcfOss.DatabaseManager.MySql.StatementAnalysis;
using TcfOss.DatabaseManager.MySql.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyTableBuilder(ObjectIdentifier name, MyConfig config, SchemaDefaults schemaDefaults, INormalizeComponents componentNormalizer, INormalizeSql expressionNormalizer, SourceRef? sourceRef)
    : TableBuilder<MyTable>(name, config.NameHandling, sourceRef)
{
    private readonly SqlValueList<MyColumn> _columns = [];
    private MyPrimaryKey? _primaryKey;
    private readonly DatabaseComponentDict<MyKey> _keys = [];
    private readonly DatabaseComponentDict<MyUniqueKey> _uniqueKeys = [];
    private readonly DatabaseComponentDict<MyForeignKey> _foreignKeys = [];
    private readonly DatabaseComponentDict<MyCheck> _checks = [];

    private string? _engine;
    private string? _characterSet;
    private string? _collation;
    private ulong? _autoIncrement;
    private Comment? _tableComment;

    private readonly QuoteStyle _quoteStyle = config.QuoteStyle;
    private readonly AttributeDefaults _attributeDefaults = config.AttributeDefaults;
    private readonly ValidationSettings _validationSettings = config.ValidationSettings;

    private readonly MyDataTypeNormalizer _dataTypeNormalizer = new(config, schemaDefaults.CharacterSet, schemaDefaults.Collation);
    private readonly INormalizeComponents _componentNormalizer = componentNormalizer;
    private readonly INormalizeSql _expressionNormalizer = expressionNormalizer;

    private readonly SchemaDefaults _schemaDefaults = schemaDefaults;

    private void AddColumn(MyColumn column, SourceRef? sourceRef)
    {
        if (!ColumnNames.Add(column.Name.Name))
        {
            throw new SqlSyntaxException.DuplicateColumn(column.Name, sourceRef);
        }
        if (column.Name.Table != Name)
        {
            throw new IdentifierMismatchException.ColumnParentMismatch(column.Name, Name);
        }
        _columns.Add(column);
    }

    public override void AddColumn(StatementColumn column, SourceRef? sourceRef)
    {
        sourceRef ??= SourceRef;
        var name = new ColumnIdentifier(column.Name.Name, Name, _quoteStyle);
        ColumnOption.Unique? unique = null;
        ColumnOption.Nullability? nullability = null;
        ColumnOption.ColumnDefault? defaultValue = null;
        ColumnOption.Generated? generated = null;
        ColumnOption.CheckConstraint? check = null;
        ColumnOption.OnUpdate? onUpdate = null;
        Comment? comment = null;
        bool? autoIncrement = null;

        foreach (StatementColumnOption opt in column.Options)
        {
            switch (opt)
            {
                case StatementColumnOption.PrimaryKey pk:
                    _primaryKey = ConvertColumnPrimaryKey(pk, name, sourceRef);
                    break;
                case StatementColumnOption.Unique u:
                    unique = ConvertColumnUnique(u, name, unique, sourceRef);
                    break;
                case StatementColumnOption.Nullability n:
                    nullability = ConvertColumnNullability(n, name, nullability, sourceRef);
                    break;
                case StatementColumnOption.Default d:
                    defaultValue = ConvertColumnDefault(d, name, defaultValue, sourceRef);
                    break;
                case StatementColumnOption.CheckConstraint ch:
                    check = ConvertColumnCheck(ch, name, check, sourceRef);
                    break;
                case StatementColumnOption.Generated gen:
                    generated = ConvertColumnGenerated(gen, name, generated, sourceRef);
                    break;
                case StatementColumnOption.ColumnComment com:
                    comment = ConvertColumnComment(com, name, comment, sourceRef);
                    break;
                case StatementColumnOption.AutoIncrement:
                    autoIncrement = ConvertColumnAutoIncrement(name, autoIncrement, sourceRef);
                    break;
                case StatementColumnOption.OnUpdate ou:
                    onUpdate = ConvertColumnOnUpdate(ou, name, onUpdate, sourceRef);
                    break;
                default:
                    throw new NotImplementedException($"{opt}");
            }
        }

        nullability ??= new ColumnOption.Nullability.Null();

        if (nullability is ColumnOption.Nullability.Null && defaultValue == null)
        {
            defaultValue = new ColumnOption.ColumnDefault.DefaultValue(new Value.Null());
        }
        else if (nullability is ColumnOption.Nullability.NotNull
            && defaultValue is ColumnOption.ColumnDefault.DefaultValue { Value: Value.Null })
        {
            defaultValue = null;
        }

        var myColumn = new MyColumn(name, GetNormalizedDataType(column.DataType))
        {
            Unique = unique,
            Nullability = nullability,
            Default = defaultValue,
            Generated = generated,
            Check = check,
            AutoIncrement = autoIncrement ?? false,
            OnUpdate = onUpdate,
            Comment = comment
        };

        AddColumn(myColumn, sourceRef);
    }

    public override void AddIndexOrConstraint(StatementTableConstraint constraint, SourceRef? sourceRef)
    {
        switch (constraint)
        {
            case StatementTableConstraint.PrimaryKey pk:
                AddPrimaryKey(pk, sourceRef);
                break;
            case StatementTableConstraint.UniqueConstraint uk:
                AddUniqueKey(uk, sourceRef);
                break;
            case StatementTableConstraint.ForeignKey fk:
                AddForeignKey(fk, sourceRef);
                break;
            case StatementTableConstraint.Check ck:
                AddCheckConstraint(ck, sourceRef);
                break;
            case StatementTableConstraint.Standard std:
                AddKey(std, sourceRef);
                break;
            case StatementTableConstraint.FullText ft:
                AddKey(ft, sourceRef);
                break;
            case StatementTableConstraint.Spatial sp:
                AddKey(sp, sourceRef);
                break;
            default:
                throw new UnreachableException();
        }
    }

    public override void AddOption(StatementTableOption tableOption)
    {
        switch (tableOption)
        {
            case MyStatementTableOption.Engine engine:
                _engine = engine.Value;
                break;
            case MyStatementTableOption.CharacterSet charset:
                _characterSet = charset.Value;
                _dataTypeNormalizer.CharacterSet = charset.Value;
                break;
            case MyStatementTableOption.Collation collation:
                _collation = collation.Value;
                _dataTypeNormalizer.Collation = collation.Value;
                break;
            case MyStatementTableOption.AutoIncrement autoIncrement:
                _autoIncrement = autoIncrement.Value;
                break;
            case MyStatementTableOption.Comment com:
                _tableComment = new Comment(com.Value);
                break;
            default:
                throw new NotImplementedException($"Not-implemented table option type {tableOption}. {SourceRef}");
        }
    }

    public void AddPrimaryKey(StatementTableConstraint.PrimaryKey pkStatement, SourceRef? sourceRef)
    {
        if (_primaryKey != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("PRIMARY KEY", "table", Name, sourceRef);
        }
        if (pkStatement.Name != null)
        {
            throw new SqlSyntaxException.ConstraintNameNotAllowedException(pkStatement.Name, "PRIMARY KEY", "table", Name, "MySQL", sourceRef);
        }

        IndexMethod indexMethod = pkStatement.IndexMethod ?? _attributeDefaults.IndexMethod;
        var primaryKey = new MyPrimaryKey(NormalizeKeyParts(pkStatement.Columns, _quoteStyle, indexMethod.ToString(), pkStatement.Name?.Name))
        {
            IndexMethod = indexMethod,
            Comment = pkStatement.Comment,
        };

        _primaryKey = primaryKey;
    }

    public void AddUniqueKey(StatementTableConstraint.UniqueConstraint ukStatement, SourceRef? sourceRef = null)
    {
        if (ukStatement.Name == null && ukStatement.IndexName == null)
        {
            throw new DefinitionException.TableConstraintNameRequired("UNIQUE constraint", Name, sourceRef);
        }

        if (ukStatement is { Name: not null, IndexName: not null } && ukStatement.Name != ukStatement.IndexName)
        {
            // throw new SqlSyntaxException.ConflictingConstraintNames(ukStatement.Name, ukStatement.IndexName, "UNIQUE constraint", Name, sourceRef);
            throw new InvalidOperationException($"UNIQUE constraint cannot have both a constraint name and an index name that are different. Constraint name: {ukStatement.Name}, index name: {ukStatement.IndexName}. Table: {Name}. Source: {sourceRef}");
        }

        Identifier name = (ukStatement.Name ?? ukStatement.IndexName!).ToSimpleIdentifier(_quoteStyle);

        IndexMethod indexMethod = ukStatement.IndexMethod ?? _attributeDefaults.IndexMethod;
        var uniqueKey = new MyUniqueKey(NormalizeKeyParts(ukStatement.Columns, _quoteStyle, indexMethod.ToString(), name.Name), name)
        {
            IndexMethod = indexMethod,
            Comment = ukStatement.Comment
        };
        _uniqueKeys[Handle.Create(name, NameHandling)] = uniqueKey;
    }

    private void AddForeignKey(StatementTableConstraint.ForeignKey fkStatement, SourceRef? sourceRef = null)
    {
        if (fkStatement.Name == null)
        {
            throw new DefinitionException.TableConstraintNameRequired("FOREIGN KEY constraint", Name, sourceRef);
        }

        Identifier name = fkStatement.Name.ToSimpleIdentifier(_quoteStyle);

        var foreignKey = new MyForeignKey(
            NormalizeIdentifierQuotations(fkStatement.Columns, _quoteStyle),
            ObjectIdentifier.FromObjectName(fkStatement.ForeignTable, Name.Schema, _quoteStyle),
            NormalizeIdentifierQuotations(fkStatement.ForeignColumns, _quoteStyle),
            name)
        {
            OnDelete = fkStatement.OnDelete ?? _attributeDefaults.ForeignKeyOnDelete,
            OnUpdate = fkStatement.OnUpdate ?? _attributeDefaults.ForeignKeyOnUpdate,
            Comment = fkStatement.Comment,
        };

        _foreignKeys[Handle.Create(fkStatement.Name, NameHandling)] = foreignKey;
    }

    private void AddCheckConstraint(StatementTableConstraint.Check checkStatement, SourceRef? sourceRef = null)
    {
        if (checkStatement.Name == null)
        {
            throw new DefinitionException.TableConstraintNameRequired("CHECK constraint", Name, sourceRef);
        }

        (Expression normalized, Expression flattened) = GetNormalizedExpressions(checkStatement.Expression);

        var check = new MyCheck(
            normalized,
            checkStatement.Name!.ToSimpleIdentifier(_quoteStyle))
        {
            Comment = checkStatement.Comment,
            NormalizedExpression = flattened
        };

        _checks[Handle.Create(checkStatement.Name!, NameHandling)] = check;
    }

    public void AddKey(StatementTableConstraint.NonConstraintKey keyStatement, SourceRef? sourceRef = null)
    {
        if (keyStatement.IndexName == null)
        {
            throw new DefinitionException.TableConstraintNameRequired("INDEX", Name, sourceRef);
        }

        MyKey key;
        switch (keyStatement)
        {
            case StatementTableConstraint.Standard k:
                IndexMethod indexMethod = k.IndexMethod ?? _attributeDefaults.IndexMethod;
                SqlValueList<KeyPart> columns = NormalizeKeyParts(k.Columns, _quoteStyle, indexMethod.ToString(), k.Name?.Name);
                key = new MyKey.Standard(columns, k.IndexName!.ToSimpleIdentifier(_quoteStyle)) { IndexMethod = indexMethod, Comment = k.Comment };
                break;
            case StatementTableConstraint.FullText k:
                SqlValueList<KeyPart> fullTextColumns = NormalizeKeyParts(k.Columns, _quoteStyle, "FULLTEXT", k.Name?.Name);
                key = new MyKey.FullText(fullTextColumns, k.IndexName!.ToSimpleIdentifier(_quoteStyle)) { Comment = k.Comment };
                break;
            case StatementTableConstraint.Spatial k:
                SqlValueList<KeyPart> spatialColumns = NormalizeKeyParts(k.Columns, _quoteStyle, "SPATIAL", k.Name?.Name);
                key = new MyKey.Spatial(spatialColumns, k.IndexName!.ToSimpleIdentifier(_quoteStyle)) { Comment = k.Comment };
                break;
            default:
                throw new UnreachableException($"Unknown key type {keyStatement.GetType()} in {nameof(TableBuilder<>)}.{nameof(AddKey)}.");
        }
        _keys[Handle.Create(keyStatement.IndexName, NameHandling)] = key;
    }

    public override PseudoTable ToPseudoTable()
    {
        return new PseudoTable(Name.Name, Name, [.. _columns.Select(c => c.Name.Name)], PseudoTableType.Table);
    }

    public override MyTable ToTable()
    {
        return new MyTable(Name, _columns)
        {
            PrimaryKey = _primaryKey,
            Keys = _keys,
            UniqueKeys = _uniqueKeys,
            ForeignKeys = _foreignKeys,
            Checks = _checks,
            Engine = _engine ?? _schemaDefaults.Engine,
            CharacterSet = _characterSet ?? _schemaDefaults.CharacterSet,
            Collation = _collation ?? _schemaDefaults.Collation,
            AutoIncrement = _autoIncrement,
            TableComment = _tableComment
        };
    }

    protected override DataType GetNormalizedDataType(DataType dataType)
    {
        dataType = _dataTypeNormalizer.NormalizeDataType(dataType);
        return dataType;
    }

    protected override Expression GetNormalizedExpression(Expression expression)
    {
        expression = _expressionNormalizer.NormalizeExpression(expression);
        return expression;
    }

    protected override (Expression Normalized, Expression Flattened) GetNormalizedExpressions(Expression expression)
    {
        Expression flattened = _expressionNormalizer.NormalizeExpression(expression);
        Expression normalized = _expressionNormalizer.NormalizeExpressionNoFlatten(expression);
        return (normalized, flattened);
    }

    private Value GetNormalizedValue(Value value)
    {
        value = _componentNormalizer.NormalizeValue(value);
        return value;
    }

    private static SqlValueList<Identifier> NormalizeIdentifierQuotations(SqlValueList<Identifier> identifiers, QuoteStyle quoteStyle)
    {
        SqlValueList<Identifier> normalized = new(identifiers.Count);
        foreach (Identifier id in identifiers)
        {
            normalized.Add(id.ToSimpleIdentifier(quoteStyle));
        }
        return normalized;
    }

    private Direction? GetIndexDirection(Direction? direction, string indexMethod, string? keyName)
    {
        if (indexMethod == "FULLTEXT" || indexMethod == "SPATIAL" || indexMethod == "HASH")
        {
            if (direction != null)
            {
                throw new SqlSyntaxException.IndexDirectionNotSupported(keyName, indexMethod, Name.ToString(), SourceRef);
            }
            return null;
        }
        else
        {
            return direction ?? Direction.Ascending;
        }
    }

    private SqlValueList<KeyPart> NormalizeKeyParts(SqlValueList<KeyPart> keyParts, QuoteStyle quoteStyle, string indexMethod, string? keyName)
    {
        SqlValueList<KeyPart> normalized = [];
        foreach (KeyPart part in keyParts)
        {
            if (part is KeyPart.Column col)
            {
                Identifier normalizedName = col.Name.ToSimpleIdentifier(quoteStyle);
                Direction? direction = GetIndexDirection(col.Direction, indexMethod, keyName);
                normalized.Add(col with { Name = normalizedName, Direction = direction });
            }
            else if (part is KeyPart.IndexExpression expr)
            {
                // The outer Nested wrapper is semantically required by MySQL functional index syntax —
                // it marks the column as an expression rather than a plain column name, so it must be
                // preserved. Only normalize the inner expression.
                Expression normalizedExpr = expr.Expression is Nested outerNested
                    ? new Nested(GetNormalizedExpression(outerNested.Expression))
                    : GetNormalizedExpression(expr.Expression);
                Direction? direction = GetIndexDirection(expr.Direction, indexMethod, keyName);
                normalized.Add(new KeyPart.IndexExpression(normalizedExpr, direction));
            }
            else
            {
                throw new UnreachableException();
            }
        }
        return normalized;
    }

    private ColumnOption.ColumnDefault NormalizeDefault(StatementColumnOption.Default givenDefault)
    {
        if (givenDefault is StatementColumnOption.Default.DefaultExpression { Expression: LiteralValue nested })
        {
            return new ColumnOption.ColumnDefault.DefaultValue(GetNormalizedValue(nested.Value));
        }

        if (givenDefault is StatementColumnOption.Default.DefaultValue dval)
        {
            return new ColumnOption.ColumnDefault.DefaultValue(GetNormalizedValue(dval.Value));
        }

        if (givenDefault is StatementColumnOption.Default.DefaultExpression dexp)
        {
            (Expression normalized, Expression flattened) = GetNormalizedExpressions(dexp.Expression);
            return new ColumnOption.ColumnDefault.DefaultExpression(normalized)
            {
                NormalizedExpression = flattened
            };
        }

        throw new NotSupportedException($"Not-implemented default option type {givenDefault}.");
    }

    private ColumnOption.Generated.AsExpression NormalizeGenerated(StatementColumnOption.Generated givenGenerated)
    {
        if (givenGenerated is StatementColumnOption.Generated.AsExpression gen)
        {
            (Expression normalized, Expression flattened) = GetNormalizedExpressions(gen.Expression);
            return new ColumnOption.Generated.AsExpression(normalized, gen.Mode)
            {
                NormalizedExpression = flattened
            };
        }
        throw new NotSupportedException($"Not-implemented generated option type {givenGenerated}.");
    }

    private MyPrimaryKey ConvertColumnPrimaryKey(StatementColumnOption.PrimaryKey primaryKey, ColumnIdentifier columnName, SourceRef? sourceRef = null)
    {
        if (_primaryKey != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("PRIMARY KEY", "column", columnName, sourceRef);
        }
        if (primaryKey.Name != null)
        {
            throw new SqlSyntaxException.ConstraintNameNotAllowedException(primaryKey.Name, "PRIMARY KEY", "column", columnName, "MySQL", sourceRef);
        }
        return new MyPrimaryKey([new KeyPart.Column(columnName.ToSimpleIdentifier(_quoteStyle))]);
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private ColumnOption.Unique ConvertColumnUnique(StatementColumnOption.Unique uniqueOption, ColumnIdentifier columnName, ColumnOption? existingUnique, SourceRef? sourceRef = null)
    {
        if (!_validationSettings.AllowUniqueOnColumn)
        {
            throw new DefinitionException.UniqueColumn(columnName, sourceRef);
        }
        if (existingUnique != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("UNIQUE", "column", columnName, sourceRef);
        }
        if (uniqueOption.Name != null && !_validationSettings.AllowNamedColumnUnique)
        {
            throw new SqlSyntaxException.ConstraintNameNotAllowedException(uniqueOption.Name, "UNIQUE", "column", columnName, "MySQL", sourceRef);
        }

        return new ColumnOption.Unique(uniqueOption.Name?.ToSimpleIdentifier(_quoteStyle));
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private ColumnOption.Nullability ConvertColumnNullability(StatementColumnOption.Nullability nullabilityOption, ColumnIdentifier columnName, ColumnOption? existingNullability, SourceRef? sourceRef = null)
    {
        if (existingNullability != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("nullability", "column", columnName, sourceRef);
        }
        if (nullabilityOption.Name != null && !_validationSettings.AllowNamedColumnNullability)
        {
            throw new SqlSyntaxException.ConstraintNameNotAllowedException(nullabilityOption.Name, "nullability", "column", columnName, "MySQL", sourceRef);
        }
        Identifier? name = nullabilityOption.Name?.ToSimpleIdentifier(_quoteStyle);
        if (nullabilityOption is StatementColumnOption.Nullability.Null)
        {
            return new ColumnOption.Nullability.Null(name);
        }
        else
        {
            return new ColumnOption.Nullability.NotNull(name);
        }
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private ColumnOption.ColumnDefault ConvertColumnDefault(StatementColumnOption.Default defaultOption, ColumnIdentifier columnName, ColumnOption? existingDefault, SourceRef? sourceRef = null)
    {
        if (existingDefault != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("default", "column", columnName, sourceRef);
        }
        if (defaultOption.Name != null && !_validationSettings.AllowNamedColumnDefault)
        {
            throw new SqlSyntaxException.ConstraintNameNotAllowedException(defaultOption.Name, "DEFAULT", "column", columnName, "MySQL", sourceRef);
        }
        defaultOption = defaultOption with { Name = defaultOption.Name?.ToSimpleIdentifier(_quoteStyle) };
        return NormalizeDefault(defaultOption);
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private ColumnOption.CheckConstraint ConvertColumnCheck(StatementColumnOption.CheckConstraint checkOption, ColumnIdentifier columnName, ColumnOption? existingCheck, SourceRef? sourceRef = null)
    {
        if (!_validationSettings.AllowCheckOnColumn)
        {
            throw new DefinitionException.CheckColumn(columnName, sourceRef);
        }
        if (existingCheck != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("CHECK", "column", columnName, sourceRef);
        }
        if (checkOption.Name != null && !_validationSettings.AllowNamedColumnCheck)
        {
            throw new SqlSyntaxException.ConstraintNameNotAllowedException(checkOption.Name, "CHECK", "column", columnName, "MySQL", sourceRef);
        }

        (Expression normalized, Expression flattened) = GetNormalizedExpressions(checkOption.Expression);
        return new ColumnOption.CheckConstraint(normalized, checkOption.Name?.ToSimpleIdentifier(_quoteStyle))
        {
            NormalizedExpression = flattened
        };
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private ColumnOption.OnUpdate ConvertColumnOnUpdate(StatementColumnOption.OnUpdate onUpdateOption, ColumnIdentifier columnName, ColumnOption? existingOnUpdate, SourceRef? sourceRef = null)
    {
        if (existingOnUpdate != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("ON UPDATE", "column", columnName, sourceRef);
        }
        Expression normalizedExpression = GetNormalizedExpression(onUpdateOption.Expression);
        if (normalizedExpression is Nested nested)
        {
            normalizedExpression = nested.Expression;
        }
        return new ColumnOption.OnUpdate(normalizedExpression);
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private ColumnOption.Generated.AsExpression ConvertColumnGenerated(StatementColumnOption.Generated generatedOption, ColumnIdentifier columnName, ColumnOption? existingGenerated, SourceRef? sourceRef = null)
    {
        if (existingGenerated != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("GENERATED", "column", columnName, sourceRef);
        }
        return NormalizeGenerated(generatedOption);
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static Comment ConvertColumnComment(StatementColumnOption.ColumnComment commentOption, ColumnIdentifier columnName, Comment? existingComment, SourceRef? sourceRef = null)
    {
        if (existingComment != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("COMMENT", "column", columnName, sourceRef);
        }
        return commentOption.Comment;
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static bool ConvertColumnAutoIncrement(ColumnIdentifier columnName, bool? existingAutoIncrement, SourceRef? sourceRef = null)
    {
        if (existingAutoIncrement != null)
        {
            throw new SqlSyntaxException.SpecifiedMoreThanOnce("AUTO_INCREMENT", "column", columnName, sourceRef);
        }
        return true;
    }
}
