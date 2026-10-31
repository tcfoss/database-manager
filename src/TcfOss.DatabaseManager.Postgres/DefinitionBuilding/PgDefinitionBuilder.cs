using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgDefinitionBuilder(PgConfig config, SourceManager sourceManager, ILogger<PgDefinitionBuilder> logger)
    : DefinitionBuilder<PgDefinition, PgConfig, PgSchemaMapping>(config, sourceManager, logger)
{
    private readonly Dictionary<ObjectHandle, Declaration<CreateType.Enumerated>> _enumeratedTypes = [];
    private readonly Dictionary<ObjectHandle, Declaration<CreateType.Composite>> _compositeTypes = [];
    private readonly Dictionary<ObjectHandle, Declaration<CreateDomain>> _domains = [];
    private readonly Dictionary<ObjectHandle, Declaration<CreateFunction>> _functions = [];
    private readonly Dictionary<ObjectHandle, Declaration<CreateProcedure>> _procedures = [];
    private readonly Dictionary<ObjectHandle, PgTableBuilder> _tableBuilders = [];
    private readonly ComponentNormalizer _componentNormalizer = new(config.QuoteStyle, new FunctionNameProvider(), config.NameHandling);

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
                        throw new NotSupportedException("PostgreSQL temporary-table definition construction is not implemented.");
                    }
                    if (table.AsSelect != null)
                    {
                        throw new DefinitionException.CreateTableAsSelect(table.Name.ToString(), sourceRef);
                    }
                    ObjectNameAndHandle nameAndHandle = GetAndValidateName(table, activeSchemaId, sourceRef);
                    var tableBuilder = new PgTableBuilder(nameAndHandle.Id, Config.NameHandling, _componentNormalizer, sourceRef);
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
        return new PgTypeResolutionManager(Config, names, _componentNormalizer);
    }

    private DatabaseObjectDict<PgCustomType> GetCustomTypes(PgTypeResolutionManager manager)
    {
        var types = new DatabaseObjectDict<PgCustomType>(Config.NameHandling);

        foreach (KeyValuePair<ObjectHandle, Declaration<CreateType.Enumerated>> declaration in _enumeratedTypes)
        {
            if (declaration.Value.Statement.Labels.Distinct(StringComparer.Ordinal).Count() != declaration.Value.Statement.Labels.Count)
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce("enum label", "type", declaration.Value.Name, declaration.Value.SourceRef);
            }
            types.Add(declaration.Key, new PgCustomType.Enumerated(manager.GetName(declaration.Key), [.. declaration.Value.Statement.Labels]));
        }
        foreach (KeyValuePair<ObjectHandle, Declaration<CreateType.Composite>> declaration in _compositeTypes)
        {
            Declaration<CreateType.Composite> item = declaration.Value;
            SqlValueList<PgCompositeTypeField> fields = [.. item.Statement.Attributes.Select(field => new PgCompositeTypeField(
                _componentNormalizer.NormalizeSingleIdentifier(field.Name), manager.Resolve(field.DataType, item.Name.Schema, item.SourceRef), field.Collation))];
            var fieldNames = new HashSet<string>();
            foreach (PgCompositeTypeField field in fields)
            {
                if (!fieldNames.Add(field.Name.Name))
                {
                    throw new SqlSyntaxException.DuplicateColumn(field.Name.Name, item.Name, item.SourceRef);
                }
            }
            types.Add(declaration.Key, new PgCustomType.Composite(manager.GetName(declaration.Key), fields));
        }
        foreach (KeyValuePair<ObjectHandle, Declaration<CreateDomain>> declaration in _domains)
        {
            Declaration<CreateDomain> item = declaration.Value;
            SqlValueList<PgDomainConstraint> constraints = [.. item.Statement.Constraints.Select(constraint => constraint switch
            {
                StatementDomainConstraint.Nullability nullability => (PgDomainConstraint)new PgDomainConstraint.Nullability(nullability.NotNull,
                    _componentNormalizer.NormalizeSingleIdentifierNullable(nullability.Name)),
                StatementDomainConstraint.Check check => new PgDomainConstraint.Check(check.Expression,
                    _componentNormalizer.NormalizeSingleIdentifierNullable(check.Name)),
                _ => throw new NotImplementedException()
            })];
            types.Add(declaration.Key, new PgCustomType.Domain(manager.GetName(declaration.Key), manager.Resolve(item.Statement.DataType, item.Name.Schema, item.SourceRef))
            {
                Collation = item.Statement.Collation,
                Default = item.Statement.Default,
                Constraints = constraints
            });
        }
        return types;
    }

    public override PgDefinition ToDefinition()
    {
        PgTypeResolutionManager manager = GetTypeResolutionManager();
        DatabaseObjectDict<PgCustomType> types = GetCustomTypes(manager);

        var tables = new DatabaseObjectDict<PgTable>(Config.NameHandling);
        foreach (KeyValuePair<ObjectHandle, PgTableBuilder> table in _tableBuilders)
        {
            tables.Add(table.Key, table.Value.ToTable(manager));
        }

        var functions = new DatabaseObjectDict<PgStoredFunction>(Config.NameHandling);
        foreach (KeyValuePair<ObjectHandle, Declaration<CreateFunction>> declaration in _functions)
        {
            Declaration<CreateFunction> item = declaration.Value;
            functions.Add(declaration.Key, new PgStoredFunction(_componentNormalizer.NormalizeObjectIdentifier(item.Name),
                ResolveParameters(item.Statement.Parameters, item.Name, item.SourceRef, manager),
                manager.Resolve(item.Statement.ReturnType, item.Name.Schema, item.SourceRef), item.Statement.Body));
        }

        var procedures = new DatabaseObjectDict<PgStoredProcedure>(Config.NameHandling);
        foreach (KeyValuePair<ObjectHandle, Declaration<CreateProcedure>> declaration in _procedures)
        {
            Declaration<CreateProcedure> item = declaration.Value;
            procedures.Add(declaration.Key, new PgStoredProcedure(_componentNormalizer.NormalizeObjectIdentifier(item.Name),
                ResolveParameters(item.Statement.Parameters, item.Name, item.SourceRef, manager), item.Statement.Body));
        }

        var definition = new PgDefinition
        {
            Tables = tables,
            CustomTypes = types,
            Functions = functions,
            Procedures = procedures
        };

        manager.ValidateDependencies(definition, SourceManager);
        List<SourcedException> errors = ValidateDefinition(definition);
        if (errors.Count != 0)
        {
            throw new ValidationExceptionSet(errors);
        }

        return definition;
    }

    private SqlValueList<PgRoutineParameter> ResolveParameters(SqlValueList<StatementRoutineParameter> parameters,
        ObjectIdentifier routine, SourceRef sourceRef, PgTypeResolutionManager manager)
    {
        var names = new HashSet<string>();
        SqlValueList<PgRoutineParameter> result = [];
        foreach (StatementRoutineParameter parameter in parameters)
        {
            Identifier name = _componentNormalizer.NormalizeSingleIdentifier(parameter.Name);
            if (!names.Add(name.Name))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(name.Name, "routine", routine, sourceRef);
            }
            DataType dataType = manager.Resolve(parameter.DataType, routine.Schema, sourceRef);
            result.Add(parameter switch
            {
                StatementRoutineParameter.Directed directed => new PgRoutineParameter(name, dataType) { Direction = directed.Direction },
                StatementRoutineParameter.Undirected undirected => new PgRoutineParameter(name, dataType)
                {
                    DefaultValue = undirected.DefaultValue,
                    Output = undirected.Output
                },
                _ => throw new NotImplementedException()
            });
        }
        return result;
    }

    protected override List<SourcedException> ValidateDefinition(PgDefinition definition)
    {
        var errors = new List<SourcedException>();
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
