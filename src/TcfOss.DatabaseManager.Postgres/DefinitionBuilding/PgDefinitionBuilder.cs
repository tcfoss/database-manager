using System.Globalization;
using Microsoft.Extensions.Logging;
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
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgDefinitionBuilder(PgConfig config, SourceManager sourceManager, ILogger<PgDefinitionBuilder> logger)
    : DefinitionBuilder<PgDefinition, PgConfig, PgSchemaMapping>(config, sourceManager, logger,
        new ComponentNormalizer(config.QuoteStyle, new FunctionNameProvider(), config.NameHandling))
{
    private readonly Dictionary<ObjectHandle, Declaration<CreateType.Enumerated>> _enumeratedTypes = [];
    private readonly Dictionary<ObjectHandle, Declaration<CreateType.Composite>> _compositeTypes = [];
    private readonly Dictionary<ObjectHandle, Declaration<CreateDomain>> _domains = [];
    private readonly Dictionary<ObjectHandle, Declaration<CreateFunction>> _functions = [];
    private readonly Dictionary<ObjectHandle, Declaration<CreateProcedure>> _procedures = [];
    private readonly Dictionary<ObjectHandle, PgTableBuilder> _tableBuilders = [];
    private readonly List<(CreateIndex Statement, SchemaIdentifier Schema, SourceRef Source)> _pendingIndexes = [];
    private readonly List<(CreateTrigger Statement, SchemaIdentifier Schema, SourceRef Source)> _pendingTriggers = [];
    private readonly List<(CommentOn Statement, SchemaIdentifier Schema, SourceRef Source)> _pendingComments = [];
    private int _appliedIndexCount;

    public override void ProcessStatements(IEnumerable<Statement> statements, SchemaIdentifier activeSchemaId, int sourceId)
    {
        foreach (Statement statement in statements)
        {
            if (statement is InertOnly)
            {
                continue;
            }
            if (statement.Meta.Start == null || statement.Meta.End == null)
            {
                throw new InvalidOperationException($"Statement is missing source position metadata: {statement.GetType().Name}");
            }
            var sourceRef = new SourceRef(sourceId, statement.Meta.Start.Value, statement.Meta.End.Value);
            switch (statement)
            {
                case CreateTable table:
                    if (table.Temporary)
                    {
                        throw new DefinitionException.TempTable(table.Name.ToString(), sourceRef);
                    }
                    if (table.AsSelect != null)
                    {
                        throw new DefinitionException.CreateTableAsSelect(table.Name.ToString(), sourceRef);
                    }
                    ObjectNameAndHandle nameAndHandle = GetAndValidateName(table, activeSchemaId, sourceRef);
                    var tableBuilder = new PgTableBuilder(nameAndHandle.Id, Config.NameHandling, ComponentNormalizer, sourceRef);
                    tableBuilder.ApplyCreateTable(table);
                    _tableBuilders.Add(nameAndHandle.Key, tableBuilder);
                    SourceManager.RegisterObject(nameAndHandle.Key, sourceRef);
                    break;
                case CreateType.Enumerated enumerated:
                    AddDeclaration(enumerated, activeSchemaId, sourceRef, _enumeratedTypes);
                    break;
                case CreateType.Composite composite:
                    AddDeclaration(composite, activeSchemaId, sourceRef, _compositeTypes);
                    break;
                case CreateDomain domain:
                    AddDeclaration(domain, activeSchemaId, sourceRef, _domains);
                    break;
                case CreateFunction function:
                    AddDeclaration(function, activeSchemaId, sourceRef, _functions);
                    break;
                case CreateProcedure procedure:
                    AddDeclaration(procedure, activeSchemaId, sourceRef, _procedures);
                    break;
                case CreateIndex index:
                    _pendingIndexes.Add((index, activeSchemaId, sourceRef));
                    break;
                case CreateTrigger trigger:
                    _pendingTriggers.Add((trigger, activeSchemaId, sourceRef));
                    break;
                case CommentOn comment:
                    _pendingComments.Add((comment, activeSchemaId, sourceRef));
                    break;
                default:
                    throw new DefinitionException($"{statement.GetType().Name} statement", sourceRef);
            }
        }
    }

    private void AddDeclaration<TStatement>(TStatement statement, SchemaIdentifier activeSchemaId, SourceRef sourceRef,
        Dictionary<ObjectHandle, Declaration<TStatement>> declarations)
        where TStatement : Statement, ICreateStatement
    {
        ObjectNameAndHandle nameAndHandle = GetAndValidateName(statement, activeSchemaId, sourceRef);
        declarations.Add(nameAndHandle.Key, new Declaration<TStatement>(nameAndHandle.Id, statement, sourceRef));
        SourceManager.RegisterObject(nameAndHandle.Key, sourceRef);
    }

    private PgTypeResolutionManager GetTypeResolutionManager()
    {
        var names = _tableBuilders.ToDictionary(entry => entry.Key, entry => entry.Value.ToPseudoTable().Identifier!);
        foreach (KeyValuePair<ObjectHandle, Declaration<CreateType.Enumerated>> declaration in _enumeratedTypes)
        {
            names.Add(declaration.Key, declaration.Value.Name);
        }
        foreach (KeyValuePair<ObjectHandle, Declaration<CreateType.Composite>> declaration in _compositeTypes)
        {
            names.Add(declaration.Key, declaration.Value.Name);
        }
        foreach (KeyValuePair<ObjectHandle, Declaration<CreateDomain>> declaration in _domains)
        {
            names.Add(declaration.Key, declaration.Value.Name);
        }
        return new PgTypeResolutionManager(Config, names, ComponentNormalizer);
    }

    private DatabaseObjectDict<PgCustomType> GetCustomTypes(PgTypeResolutionManager manager)
    {
        var types = new DatabaseObjectDict<PgCustomType>(Config.NameHandling);

        foreach ((ObjectHandle handle, Declaration<CreateType.Enumerated> declaration) in _enumeratedTypes)
        {
            if (declaration.Statement.Labels.Distinct(StringComparer.Ordinal).Count() != declaration.Statement.Labels.Count)
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce("enum label", "type", declaration.Name, declaration.SourceRef);
            }
            types.Add(handle, new PgCustomType.Enumerated(manager.GetName(handle), [.. declaration.Statement.Labels]));
        }

        foreach ((ObjectHandle handle, Declaration<CreateType.Composite> declaration) in _compositeTypes)
        {
            SqlValueList<PgCompositeTypeField> fields = [.. declaration.Statement.Attributes.Select(field => new PgCompositeTypeField(
                ComponentNormalizer.NormalizeSingleIdentifier(field.Name), manager.Resolve(field.DataType, declaration.Name.Schema, declaration.SourceRef), field.Collation))];
            var fieldNames = new HashSet<string>();
            foreach (PgCompositeTypeField field in fields)
            {
                if (!fieldNames.Add(field.Name.Name))
                {
                    throw new SqlSyntaxException.DuplicateColumn(field.Name.Name, declaration.Name, declaration.SourceRef);
                }
            }
            types.Add(handle, new PgCustomType.Composite(manager.GetName(handle), fields));
        }

        foreach ((ObjectHandle handle, Declaration<CreateDomain> declaration) in _domains)
        {
            SqlValueList<PgDomainConstraint> constraints = [.. declaration.Statement.Constraints.Select<StatementDomainConstraint, PgDomainConstraint>(constraint => constraint switch
            {
                StatementDomainConstraint.Nullability nullability => new PgDomainConstraint.Nullability(nullability.NotNull,
                    ComponentNormalizer.NormalizeSingleIdentifierNullable(nullability.Name)),
                StatementDomainConstraint.Check check => new PgDomainConstraint.Check(check.Expression,
                    ComponentNormalizer.NormalizeSingleIdentifierNullable(check.Name)),
                _ => throw new NotImplementedException()
            })];
            types.Add(handle, new PgCustomType.Domain(manager.GetName(handle), manager.Resolve(declaration.Statement.DataType, declaration.Name.Schema, declaration.SourceRef))
            {
                Collation = declaration.Statement.Collation,
                Default = declaration.Statement.Default,
                Constraints = constraints
            });
        }

        return types;
    }

    private PgRoutineOptions? NormalizeRoutineOptions(SqlValueList<PgStatementRoutineOption>? beforeBody,
        SqlValueList<PgStatementRoutineOption>? afterBody, Statement body, ObjectIdentifier routine, SourceRef sourceRef,
        FunctionReturn? returnType)
    {
        PgStatementRoutineOption[] statements = [.. beforeBody ?? [], .. afterBody ?? []];
        if (statements.Length == 0 && body is not (BeginAtomic or Return))
        {
            return null;
        }
        var language = new Identifier("sql", QuoteStyle.None);
        SecurityContext security = SecurityContext.Invoker;
        SqlValueList<PgRoutineSetting> settings = [];
        PgRoutineVolatility volatility = PgRoutineVolatility.Volatile;
        bool strict = false;
        PgRoutineParallel parallel = PgRoutineParallel.Unsafe;
        float? cost = null;
        float? rows = null;
        foreach (PgStatementRoutineOption statement in statements)
        {
            switch (statement)
            {
                case PgStatementRoutineOption.Language languageOption:
                    language = languageOption.Name;
                    break;
                case PgStatementRoutineOption.Security securityOption:
                    security = securityOption.Context;
                    break;
                case PgStatementRoutineOption.RoutineSetting setting:
                    PgRoutineSetting canonicalSetting = setting.Setting switch
                    {
                        PgRoutineSetting.ToValues values => values with { Values = [.. values.Values] },
                        PgRoutineSetting.FromCurrent current => throw new DefinitionException.RoutineSettingFromCurrent(routine, current.Name, sourceRef),
                        _ => throw new DefensiveGuardException("routine setting type", setting.Setting.GetType())
                    };
                    settings.Add(canonicalSetting);
                    break;
                default:
                    if (returnType == null)
                    {
                        throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"procedure option {statement.UniqueName}", sourceRef);
                    }
                    switch (statement)
                    {
                        case PgStatementRoutineOption.Volatility volatilityOption:
                            volatility = volatilityOption.Mode;
                            break;
                        case PgStatementRoutineOption.NullHandling nullHandling:
                            strict = nullHandling.Mode != PgRoutineNullHandling.CalledOnNullInput;
                            break;
                        case PgStatementRoutineOption.Parallel parallelOption:
                            parallel = parallelOption.Mode;
                            break;
                        case PgStatementRoutineOption.Cost costOption:
                            cost = NormalizeRoutineEstimate(costOption.Value, costOption.UniqueName, sourceRef);
                            break;
                        case PgStatementRoutineOption.Rows rowsOption:
                            rows = NormalizeRoutineEstimate(rowsOption.Value, rowsOption.UniqueName, sourceRef);
                            break;
                        default:
                            throw new DefensiveGuardException("routine option type", statement.GetType());
                    }
                    break;
            }
        }
        language = ComponentNormalizer.NormalizeSingleIdentifier(language);
        if (returnType == null)
        {
            return new PgRoutineOptions.Procedure(language) { Security = security, Settings = settings };
        }
        bool returnsSet = returnType is FunctionReturn.SetOf or FunctionReturn.Table;
        if (rows.HasValue && !returnsSet)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "ROWS on a function that does not return a set", sourceRef);
        }
        return new PgRoutineOptions.Function(language, cost ?? (language.Name is "c" or "internal" ? 1 : 100),
            rows ?? (returnsSet ? 1000 : 0))
        {
            Security = security,
            Settings = settings,
            Volatility = volatility,
            Strict = strict,
            Parallel = parallel
        };
    }

    private static float NormalizeRoutineEstimate(Value.Number number, string option, SourceRef sourceRef)
    {
        if (!float.TryParse(number.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float estimate)
            || !float.IsFinite(estimate) || estimate <= 0)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"{option} {number.Value}", sourceRef);
        }
        return estimate;
    }

    private FunctionReturn ResolveFunctionReturn(StatementFunctionReturn? returnType, SqlValueList<RoutineParameter.Postgres> parameters,
        ObjectIdentifier routine, SourceRef sourceRef, PgTypeResolutionManager manager)
    {
        RoutineParameter.Postgres[] outputParameters = [.. parameters.Where(parameter => parameter.Direction == RoutineParameterDirection.Out || parameter.Direction == RoutineParameterDirection.InOut)];

        if (returnType == null)
        {
            return outputParameters.Length switch
            {
                1 => new FunctionReturn.DataTypeReturn(outputParameters[0].DataType),
                > 1 => new FunctionReturn.PseudoType(PgPseudoType.Record),
                _ => throw new SqlSyntaxException.UnsupportedDialect("PostgreSQL", "a function without RETURNS or output parameters", sourceRef)
            };
        }

        FunctionReturn resolved = ResolveFunctionReturnShape(returnType, routine, sourceRef, manager);
        if (outputParameters.Length == 0)
        {
            return resolved;
        }
        if (returnType is StatementFunctionReturn.Table)
        {
            throw new SqlSyntaxException.UnsupportedDialect("PostgreSQL", "RETURNS TABLE with OUT or INOUT parameters", sourceRef);
        }
        FunctionReturn outputType = resolved is FunctionReturn.SetOf set ? set.ReturnType : resolved;
        bool matchesOutput = outputParameters.Length == 1
            ? outputType is FunctionReturn.DataTypeReturn scalar && scalar.DataType == outputParameters[0].DataType
            : outputType is FunctionReturn.PseudoType { Type: var type } && type == PgPseudoType.Record;
        if (!matchesOutput)
        {
            throw new SqlSyntaxException.UnsupportedDialect("PostgreSQL", "a RETURNS clause that conflicts with OUT or INOUT parameters", sourceRef);
        }
        return resolved;
    }

    private FunctionReturn ResolveFunctionReturnShape(StatementFunctionReturn returnType, ObjectIdentifier routine,
        SourceRef sourceRef, PgTypeResolutionManager manager)
    {
        switch (returnType)
        {
            case StatementFunctionReturn.DataTypeReturn scalar:
                return new FunctionReturn.DataTypeReturn(manager.Resolve(scalar.DataType, routine.Schema, sourceRef));
            case StatementFunctionReturn.PseudoType pseudo:
                return new FunctionReturn.PseudoType(pseudo.Type);
            case StatementFunctionReturn.SetOf set:
                return new FunctionReturn.SetOf((FunctionReturn.Element)ResolveFunctionReturnShape(set.ReturnType, routine, sourceRef, manager));
            case StatementFunctionReturn.Table table:
                var names = new HashSet<Handle>();
                var columns = new SqlValueList<FunctionReturn.TableColumn>();
                foreach (StatementFunctionReturn.TableColumn column in table.Columns)
                {
                    if (!names.Add(Handle.Create(column.Name, Config.NameHandling)))
                    {
                        SourceRef location = column.Name.Source is { } columnSource
                            ? columnSource with { SourceId = sourceRef.SourceId }
                            : sourceRef;
                        throw new SqlSyntaxException.SpecifiedMoreThanOnce("return column " + column.Name.Name, "function", routine, location);
                    }
                    columns.Add(new FunctionReturn.TableColumn(ComponentNormalizer.NormalizeSingleIdentifier(column.Name),
                        manager.Resolve(column.DataType, routine.Schema, sourceRef)));
                }
                return new FunctionReturn.Table(columns);
            default:
                throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"function return shape {returnType.GetType().Name}", sourceRef);
        }
    }

    public override PgDefinition ToDefinition()
    {
        PgTypeResolutionManager manager = GetTypeResolutionManager();
        DatabaseObjectDict<PgCustomType> types = GetCustomTypes(manager);
        ApplyIndexes(manager);

        var tables = new DatabaseObjectDict<PgTable>(Config.NameHandling);
        foreach (KeyValuePair<ObjectHandle, PgTableBuilder> table in _tableBuilders)
        {
            tables.Add(table.Key, table.Value.ToTable(manager));
        }
        ValidateRelationNames(tables);

        var functions = new DatabaseObjectDict<PgStoredFunction>(Config.NameHandling);
        foreach (KeyValuePair<ObjectHandle, Declaration<CreateFunction>> declaration in _functions)
        {
            Declaration<CreateFunction> item = declaration.Value;
            SqlValueList<RoutineParameter.Postgres> parameters = ResolveParameters(item.Statement.Parameters, item.Name, item.SourceRef, manager);
            FunctionReturn returnType = ResolveFunctionReturn(item.Statement.ReturnType, parameters, item.Name, item.SourceRef, manager);
            functions.Add(declaration.Key, new PgStoredFunction(item.Name,
                parameters, returnType, item.Statement.Body)
            {
                Options = (PgRoutineOptions.Function?)NormalizeRoutineOptions(item.Statement.PgOptionsBeforeBody,
                    item.Statement.PgOptionsAfterBody, item.Statement.Body, item.Name, item.SourceRef, returnType),
                RawBodyText = SourceManager.GetText(item.SourceRef,
                    item.Statement.RoutineTailMeta.Start != null ? item.Statement.RoutineTailMeta : item.Statement.Body.Meta)
            });
        }

        var procedures = new DatabaseObjectDict<PgStoredProcedure>(Config.NameHandling);
        foreach ((ObjectHandle key, Declaration<CreateProcedure> item) in _procedures)
        {
            procedures.Add(key, new PgStoredProcedure(item.Name,
                ResolveParameters(item.Statement.Parameters, item.Name, item.SourceRef, manager), item.Statement.Body)
            {
                Options = (PgRoutineOptions.Procedure?)NormalizeRoutineOptions(item.Statement.PgOptionsBeforeBody,
                    item.Statement.PgOptionsAfterBody, item.Statement.Body, item.Name, item.SourceRef, null),
                RawBodyText = SourceManager.GetText(item.SourceRef,
                    item.Statement.RoutineTailMeta.Start != null ? item.Statement.RoutineTailMeta : item.Statement.Body.Meta)
            });
        }

        var triggerResolver = new PgTriggerResolutionManager(Config, ComponentNormalizer, tables, functions);
        var triggers = new DatabaseObjectDict<DatabaseComponentDict<PgTrigger>>(Config.NameHandling);
        foreach ((CreateTrigger Statement, SchemaIdentifier Schema, SourceRef Source) pending in _pendingTriggers)
        {
            PgTrigger trigger = triggerResolver.Resolve(pending.Statement, pending.Schema, pending.Source);
            if (!triggers.TryGetValue(trigger.OnTable, out DatabaseComponentDict<PgTrigger>? tableTriggers))
            {
                tableTriggers = new DatabaseComponentDict<PgTrigger>(Config.NameHandling);
                triggers.Add(trigger.OnTable, tableTriggers);
            }
            Handle triggerName = Handle.Create(trigger.Name, Config.NameHandling);
            if (tableTriggers.ContainsKey(triggerName))
            {
                throw new SqlSyntaxException.DuplicateObject(trigger.Name.Name + " ON " + trigger.OnTable, pending.Source);
            }
            tableTriggers.Add(triggerName, trigger);
        }

        var definition = new PgDefinition
        {
            Tables = tables,
            CustomTypes = types,
            Functions = functions,
            Procedures = procedures,
            Triggers = triggers
        };

        var commentResolver = new PgCommentResolutionManager(Config, definition, manager);
        var commentTargets = new HashSet<PgCommentTarget>();
        foreach ((CommentOn Statement, SchemaIdentifier Schema, SourceRef Source) pending in _pendingComments)
        {
            PgCommentTarget target = commentResolver.Resolve(pending.Statement.Target, pending.Schema, pending.Source);
            if (!commentTargets.Add(target))
            {
                throw new SqlSyntaxException.DuplicateObject("COMMENT ON " + target.ToStatementTarget().ToSql(), pending.Source);
            }
            string? comment = pending.Statement.Comment switch
            {
                Value.SingleQuotedString literal => literal.Value.Replace("''", "'", StringComparison.Ordinal),
                Value.EscapeString literal => literal.Decode(),
                Value.DollarQuotedString literal => literal.Value,
                Value.Null => null,
                _ => throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "non-string COMMENT ON value", pending.Source)
            };
            if (comment is { Length: > 0 })
            {
                definition.Comments.Add(target, comment);
            }
        }

        manager.ValidateDependencies(definition, SourceManager);
        List<SourcedException> errors = ValidateDefinition(definition);
        if (errors.Count != 0)
        {
            throw new ValidationExceptionSet(errors);
        }

        return definition;
    }

    private void ApplyIndexes(PgTypeResolutionManager manager)
    {
        while (_appliedIndexCount < _pendingIndexes.Count)
        {
            (CreateIndex Statement, SchemaIdentifier Schema, SourceRef Source) pending = _pendingIndexes[_appliedIndexCount];
            CreateIndex statement = pending.Statement;
            ObjectIdentifier tableName;
            try
            {
                tableName = ((PgDataType.Custom)manager.Resolve(new DataTypeReference.Named(statement.TableName), pending.Schema, pending.Source)).Name;
            }
            catch (SqlSyntaxException.TypeNotFound)
            {
                throw new SqlSyntaxException.CreateIndexUnknownTable(ObjectIdentifier.FromObjectName(statement.TableName, pending.Schema, null), pending.Source);
            }
            ObjectHandle tableHandle = ObjectHandle.Create(tableName, Config.NameHandling);
            if (!_tableBuilders.TryGetValue(tableHandle, out PgTableBuilder? tableBuilder))
            {
                throw new SqlSyntaxException.CreateIndexUnknownTable(tableName, pending.Source);
            }
            tableBuilder.AddIndex(statement, pending.Source);
            _appliedIndexCount++;
        }
    }

    private void ValidateRelationNames(DatabaseObjectDict<PgTable> tables)
    {
        var names = tables.Keys.ToHashSet();
        foreach (KeyValuePair<ObjectHandle, PgTable> table in tables)
        {
            IEnumerable<Identifier> relationNames = table.Value.Indexes.Values.Select(index => index.Name)
                .Concat(table.Value.UniqueConstraints.Values.Select(unique => unique.Name!));
            if (table.Value.PrimaryKey?.Name is { } primaryKeyName)
            {
                relationNames = relationNames.Append(primaryKeyName);
            }
            foreach (Identifier relationName in relationNames)
            {
                ObjectHandle handle = ObjectHandle.Create(
                    new ObjectIdentifier(relationName.Name, table.Value.Name.Schema, relationName.QuoteStyle), Config.NameHandling);
                SourceRef? source = SourceManager.GetSourceRef(handle) ?? SourceManager.GetSourceRef(table.Key);
                if (!names.Add(handle))
                {
                    throw new SqlSyntaxException.DuplicateObject(relationName, source);
                }
                if (SourceManager.GetSourceRef(handle) == null)
                {
                    SourceManager.RegisterObject(handle, source ?? throw new InvalidOperationException("Table source metadata is missing."));
                }
            }
        }
    }

    private SqlValueList<RoutineParameter.Postgres> ResolveParameters(SqlValueList<StatementRoutineParameter> parameters,
        ObjectIdentifier routine, SourceRef sourceRef, PgTypeResolutionManager manager)
    {
        var names = new HashSet<string>();
        SqlValueList<RoutineParameter.Postgres> result = [];
        bool hasInputDefault = false;
        bool hasVariadic = false;
        foreach (StatementRoutineParameter parameter in parameters)
        {
            Identifier? name = parameter.Name is { } parameterName
                ? ComponentNormalizer.NormalizeSingleIdentifier(parameterName)
                : null;
            if (name != null && !names.Add(name.Name))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(name.Name, "routine", routine, sourceRef);
            }
            DataType dataType = manager.Resolve(parameter.DataType, routine.Schema, sourceRef);
            switch (parameter)
            {
                case StatementRoutineParameter.General { Options: not null }:
                case StatementRoutineParameter.Undirected:
                    throw new SqlSyntaxException.UnsupportedDialect("PostgreSQL", "SQL Server routine parameter options", sourceRef);
                case StatementRoutineParameter.General or StatementRoutineParameter.Directed:
                    break;
                default:
                    throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"routine parameter {parameter.GetType().Name}", sourceRef);
            }
            RoutineParameterDirection direction = parameter.Direction ?? RoutineParameterDirection.In;
            Expression? defaultExpression = ResolveRoutineParameterDefault(parameter.ParameterDefault);

            bool isInput = direction != RoutineParameterDirection.Out;
            if (direction == RoutineParameterDirection.Variadic)
            {
                if (hasVariadic)
                {
                    throw new SqlSyntaxException.SpecifiedMoreThanOnce("VARIADIC", "routine", routine, sourceRef);
                }
                if (dataType is not PgDataType.Array)
                {
                    throw new SqlSyntaxException.UnsupportedDialect("PostgreSQL", "a non-array VARIADIC parameter", sourceRef);
                }
                hasVariadic = true;
            }
            else if (hasVariadic && isInput)
            {
                throw new SqlSyntaxException.UnsupportedDialect("PostgreSQL", "input parameters after VARIADIC", sourceRef);
            }
            if (direction == RoutineParameterDirection.Out && defaultExpression != null)
            {
                throw new SqlSyntaxException.UnsupportedDialect("PostgreSQL", "defaults on OUT parameters", sourceRef);
            }
            if (isInput)
            {
                if (defaultExpression == null && hasInputDefault)
                {
                    throw new SqlSyntaxException.UnsupportedDialect("PostgreSQL", "an input parameter without a default after a parameter with a default", sourceRef);
                }
                hasInputDefault |= defaultExpression != null;
            }
            result.Add(new RoutineParameter.Postgres(name, dataType, direction, defaultExpression));
        }
        return result;
    }

    private static Expression? ResolveRoutineParameterDefault(ParameterDefault? parameterDefault)
    {
        return parameterDefault switch
        {
            ParameterDefault.ExpressionDefault expression => expression.Expression,
            ParameterDefault.ValueDefault value => new LiteralValue(value.Value),
            null => null,
            _ => throw new NotImplementedException()
        };
    }

    protected override List<SourcedException> ValidateDefinition(PgDefinition definition)
    {
        var errors = new List<SourcedException>();
        foreach (KeyValuePair<ObjectHandle, PgTable> entry in definition.Tables)
        {
            PgTable table = entry.Value;
            foreach (PgTableConstraint.ForeignKey foreignKey in table.ForeignKeys.Values)
            {
                string keyName = foreignKey.Name?.Name ?? "foreign key";
                SourceRef? source = SourceManager.GetSourceRef(entry.Key);
                if (!definition.Tables.TryGetValue(foreignKey.ReferencedTable, out PgTable? referencedTable))
                {
                    errors.Add(new SqlSyntaxException.ForeignKeyReferencedTableNotFound(keyName, table.Name, foreignKey.ReferencedTable, source));
                    continue;
                }
                if (foreignKey.Columns.Count != foreignKey.ReferencedColumns.Count)
                {
                    errors.Add(new SqlSyntaxException.ForeignKeyColumnCountMismatch(keyName, table.Name,
                        foreignKey.Columns.Count, foreignKey.ReferencedColumns.Count, source));
                    continue;
                }
                var referencedNames = referencedTable.Columns.Select(column => column.Name.Name).ToHashSet();
                bool missing = false;
                foreach (Identifier column in foreignKey.ReferencedColumns)
                {
                    if (!referencedNames.Contains(column.Name))
                    {
                        errors.Add(new SqlSyntaxException.ForeignKeyReferencedColumnNotFound(keyName, table.Name, referencedTable.Name, column.Name, source));
                        missing = true;
                    }
                }
                if (missing)
                {
                    continue;
                }
                var referencedColumns = foreignKey.ReferencedColumns.Select(column => column.Name).ToHashSet();
                bool backed = referencedTable.PrimaryKey is { } primaryKey
                    && primaryKey.Columns.Count == foreignKey.ReferencedColumns.Count
                    && referencedColumns.SetEquals(primaryKey.Columns.Select(column => column.Name));
                backed |= referencedTable.UniqueConstraints.Values.Any(unique => unique.Columns.Count == foreignKey.ReferencedColumns.Count
                    && referencedColumns.SetEquals(unique.Columns.Select(column => column.Name)));
                backed |= referencedTable.Indexes.Values.Any(index => index is { Unique: true, Predicate: null }
                                                                      && index.Columns.Count == foreignKey.ReferencedColumns.Count
                                                                      && index.Columns.All(part => part is KeyPart.Column)
                                                                      && referencedColumns.SetEquals(index.Columns.Cast<KeyPart.Column>().Select(column => column.Name.Name)));
                if (!backed)
                {
                    errors.Add(new DefinitionException.ForeignKeyNoReferencedBackingIndex(keyName, table.Name, referencedTable.Name, source));
                }
            }
        }
        foreach (KeyValuePair<ObjectHandle, PgCustomType> entry in definition.CustomTypes)
        {
            if (entry.Value is not PgCustomType.Domain domain)
            {
                continue;
            }
            var names = new HashSet<string>();
            bool? notNull = null;
            foreach (PgDomainConstraint constraint in domain.Constraints)
            {
                if (constraint is not PgDomainConstraint.Nullability { NotNull: false }
                    && constraint.Name != null && !names.Add(constraint.Name.Name))
                {
                    errors.Add(new SqlSyntaxException.SpecifiedMoreThanOnce(constraint.Name.Name, "domain", domain.Name, SourceManager.GetSourceRef(entry.Key)));
                }
                if (constraint is PgDomainConstraint.Nullability nullability)
                {
                    if (notNull.HasValue && (notNull.Value || nullability.NotNull))
                    {
                        errors.Add(new SqlSyntaxException.SpecifiedMoreThanOnce("nullability", "domain", domain.Name, SourceManager.GetSourceRef(entry.Key)));
                    }
                    notNull = nullability.NotNull;
                }
            }
        }
        return errors;
    }

    protected override List<PseudoTable> GetPseudoTables()
    {
        return [.. _tableBuilders.Values.Select(table => table.ToPseudoTable())];
    }

    private sealed record Declaration<TStatement>(ObjectIdentifier Name, TStatement Statement, SourceRef SourceRef)
        where TStatement : Statement, ICreateStatement;
}
