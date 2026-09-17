using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.StatementAnalysis;
using TcfOss.DataStructures.ValueCollections;

using DeployScript = TcfOss.DatabaseManager.Core.DefinitionMapping.DeployScript;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms;

public partial class MyDbDefinitionLoader(
    MyConfig config,
    IRetrieveDatabaseObjects repo,
    IParseText textParser,
    IFunctionNameProvider functionNameProvider,
    ILogger<MyDbDefinitionLoader> logger)
    : ILoadDbDefinition<MyDefinition>, IGetUnappliedRefactors, IGetUnappliedDeployScripts
{
    private readonly MyConfig _config = config;
    private readonly IRetrieveDatabaseObjects _repo = repo;
    private readonly IFunctionNameProvider _functionNameProvider = functionNameProvider;
    private readonly ILogger<MyDbDefinitionLoader> _logger = logger;
    private readonly IParseText _tp = textParser;

    public async Task<IEnumerable<string>> GetUnappliedRefactorIdsAsync(SchemaIdentifier schemaId, IReadOnlyCollection<string> allRefactorIds)
    {
        s_logRetrievingUnappliedRefactors(_logger, schemaId);
        return await _repo.GetUnappliedRefactorsAsync(schemaId, allRefactorIds);
    }

    public async Task<List<Refactor>> GetAllUnappliedRefactorsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds)
    {
        List<Refactor> allUnappliedRefactors = [];
        foreach (SchemaIdentifier schemaId in schemaIds)
        {
            MySchemaMapping schema = _config.Schemas[schemaId];
            Refactor[] schemaRefactors = schema.Refactors;
            IEnumerable<string> unappliedRefactors = await GetUnappliedRefactorIdsAsync(schemaId, schemaRefactors.Select(r => r.UniqueId).ToArray());
            allUnappliedRefactors.AddRange(schemaRefactors.Where(r => unappliedRefactors.Contains(r.UniqueId)));
        }
        return allUnappliedRefactors;
    }

    public async Task<List<DeployScript>> GetAllUnappliedDeployScriptsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds)
    {
        List<DeployScript> deployScripts = [];

        string[] givenIds = [.. schemaIds
            .SelectMany(s => _config.Schemas[s].DeployScripts)
            .Where(ds => ds.UniqueId != null)
            .Select(ds => ds.UniqueId!)];

        var unappliedDeployScriptIds = new HashSet<string>(await _repo.GetUnappliedDeployScriptsAsync(schemaIds, givenIds));

        foreach (SchemaIdentifier schemaId in schemaIds)
        {
            MySchemaMapping schema = _config.Schemas[schemaId];

            foreach (Core.Configuration.DeployScript deployScript in schema.DeployScripts)
            {
                if (deployScript.UniqueId != null && !unappliedDeployScriptIds.Contains(deployScript.UniqueId))
                {
                    continue;
                }
                string bodyText = await File.ReadAllTextAsync(deployScript.FilePath);
                SqlValueList<Statement> statements = _tp.ParseText(bodyText, deployScript.FilePath);
                foreach (Statement statement in statements)
                {
                    if (statement is IHaveBodyStatement haveBodyStatement)
                    {
                        statement.Meta.RawText = SourceManager.GetText(bodyText, haveBodyStatement.Body.Meta);
                    }
                    else
                    {
                        statement.Meta.RawText = SourceManager.GetText(bodyText, statement.Meta);
                    }
                }
                deployScripts.Add(new DeployScript(deployScript.Type, schema.SchemaName, deployScript.FileName, deployScript.FilePath, statements)
                {
                    UniqueId = deployScript.UniqueId,
                    RawBodyText = bodyText
                });
            }
        }
        return deployScripts;
    }

    private async Task<MySchema> LoadSchemaAsync(SchemaIdentifier schemaId, IReadOnlyCollection<string> excludedNames, CancellationToken cancellationToken = default)
    {
        s_loadingSchemaFromDatabase(_logger, schemaId);

        List<ObjectIdentifier> tableIds = await _repo.GetTableIdentifiersAsync(schemaId, excludedNames, cancellationToken: cancellationToken);
        List<ObjectIdentifier> functionIds = await _repo.GetFunctionIdentifiersAsync(schemaId, excludedNames, cancellationToken);
        List<ObjectIdentifier> procedureIds = await _repo.GetProcedureIdentifiersAsync(schemaId, excludedNames, cancellationToken);
        List<ObjectIdentifier> viewIds = await _repo.GetViewIdentifiersAsync(schemaId, excludedNames, cancellationToken);
        List<ObjectIdentifier> eventIds = await _repo.GetEventIdentifiersAsync(schemaId, excludedNames, cancellationToken);

        IEnumerable<Task<(ObjectIdentifier tableId, MyTable table, Dictionary<ObjectIdentifier, MyTrigger> trig)>> tableTasks = tableIds.Select(async tableId =>
        {
            MyTable table = await _repo.GetTableAsync(tableId, cancellationToken);
            Dictionary<ObjectIdentifier, MyTrigger> trig = await _repo.GetTriggersOnTableAsync(tableId, cancellationToken);
            return (tableId, table, trig);
        });
        (ObjectIdentifier tableId, MyTable table, Dictionary<ObjectIdentifier, MyTrigger> trig)[] tableResults = await Task.WhenAll(tableTasks);

        var tables = new ValueDict<ObjectIdentifier, MyTable>();
        var triggers = new ValueDict<ObjectIdentifier, MyTrigger>();
        foreach ((ObjectIdentifier tableId, MyTable table, Dictionary<ObjectIdentifier, MyTrigger> trigMap) in tableResults)
        {
            tables.Add(tableId, table);
            foreach ((ObjectIdentifier trigId, MyTrigger trig) in trigMap)
            {
                triggers.Add(trigId, trig);
            }
        }

        IEnumerable<Task<MyStoredFunction>> functionTasks = functionIds.Select(async id => await _repo.GetFunctionAsync(id, cancellationToken));
        MyStoredFunction[] functionResults = await Task.WhenAll(functionTasks);

        var functions = new ValueDict<ObjectIdentifier, MyStoredFunction>();
        for (int i = 0; i < functionIds.Count; i++)
        {
            functions.Add(functionIds[i], functionResults[i]);
        }

        IEnumerable<Task<MyStoredProcedure>> procedureTasks = procedureIds.Select(async id => await _repo.GetProcedureAsync(id, cancellationToken));
        MyStoredProcedure[] procedureResults = await Task.WhenAll(procedureTasks);

        var procedures = new ValueDict<ObjectIdentifier, MyStoredProcedure>();
        for (int i = 0; i < procedureIds.Count; i++)
        {
            procedures.Add(procedureIds[i], procedureResults[i]);
        }

        IEnumerable<Task<MyView>> viewTasks = viewIds.Select(async id => await _repo.GetViewAsync(id, cancellationToken));
        MyView[] viewResults = await Task.WhenAll(viewTasks);

        var views = new ValueDict<ObjectIdentifier, MyView>();
        for (int i = 0; i < viewIds.Count; i++)
        {
            views.Add(viewIds[i], viewResults[i]);
        }

        IEnumerable<Task<MyEvent>> eventTasks = eventIds.Select(async id => await _repo.GetEventAsync(id, cancellationToken));
        MyEvent[] eventResults = await Task.WhenAll(eventTasks);

        var events = new ValueDict<ObjectIdentifier, MyEvent>();
        for (int i = 0; i < eventIds.Count; i++)
        {
            events.Add(eventIds[i], eventResults[i]);
        }

        return new MySchema(schemaId)
        {
            Tables = tables,
            Procedures = procedures,
            Functions = functions,
            Views = views,
            Triggers = triggers,
            Events = events
        };
    }

    private async Task<MyDefinition> LoadDefinitionAsync(IEnumerable<MySchemaMapping> schemaMaps, CancellationToken cancellationToken = default)
    {
        var definition = new MyDefinition();

        foreach (MySchemaMapping mapping in schemaMaps)
        {
            MySchema currentSchema = await LoadSchemaAsync(mapping.SchemaName, mapping.ExcludeDatabaseObjectNames, cancellationToken);
            foreach ((ObjectIdentifier tableId, MyTable table) in currentSchema.Tables)
            {
                definition.Tables.Add(tableId, table);
            }
            foreach ((ObjectIdentifier procedureId, MyStoredProcedure procedure) in currentSchema.Procedures)
            {
                definition.Procedures.Add(procedureId, procedure);
            }
            foreach ((ObjectIdentifier functionId, MyStoredFunction function) in currentSchema.Functions)
            {
                definition.Functions.Add(functionId, function);
            }
            foreach ((ObjectIdentifier viewId, MyView view) in currentSchema.Views)
            {
                definition.Views.Add(viewId, view);
            }
            foreach ((ObjectIdentifier triggerId, MyTrigger trigger) in currentSchema.Triggers)
            {
                definition.Triggers.Add(triggerId, trigger);
            }
            foreach ((ObjectIdentifier eventId, MyEvent evt) in currentSchema.Events)
            {
                definition.Events.Add(eventId, evt);
            }
        }

        NormalizeDefinition(definition);

        s_logLoadedDefinition(_logger, new LogDelegateWrapper<MyDefinition>(definition, _logger.IsEnabled(LogLevel.Trace), DefinitionHelpers.SortDefinition));

        return definition;
    }

    public Task<MyDefinition> LoadDefinitionAsync(CancellationToken cancellationToken = default)
    {
        return LoadDefinitionAsync(_config.Schemas.Values, cancellationToken);
    }

    private void NormalizeDefinition(MyDefinition definition)
    {
        List<PseudoTable> allPseudoTables = [
            .. definition.Tables.Values.Select(t => t.ToPseudoTable()),
            .. definition.Views.Values.Select(v => v.ToPseudoTable()),
        ];
        var componentNormalizer = new MyComponentNormalizer(_config.QuoteStyle, _functionNameProvider);
        var normalizer = new MyNormalizer(_config, componentNormalizer);

        foreach (ObjectHandle key in definition.Views.Keys.ToList())
        {
            MyView view = definition.Views[key];
            try
            {
                var pseudoTableSet = new PseudoTableSet(view.Name.Schema, allPseudoTables);
                definition.Views[key] = view with
                {
                    NormalizedBody = normalizer.NormalizeSelect(view.Body, view.Name.Schema, pseudoTableSet, view.Name)
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to normalize IS view '{view.Name}'.", ex);
            }
        }

        foreach (MyTable table in definition.Tables.Values)
        {
            try
            {
                NormalizeTableInPlace(table, normalizer);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to normalize IS table '{table.Name}'.", ex);
            }
        }
    }

    private static void NormalizeTableInPlace(MyTable table, MyNormalizer normalizer)
    {
        for (int i = 0; i < table.Columns.Count; i++)
        {
            table.Columns[i] = NormalizeColumn(table.Columns[i], normalizer);
        }
        foreach (Handle key in table.Checks.Keys)
        {
            Expression normalizedExpr = normalizer.NormalizeExpressionNoFlatten(table.Checks[key].Expression);
            Expression flattenedExpr = normalizer.NormalizeExpression(table.Checks[key].Expression);
            table.Checks[key] = table.Checks[key] with
            {

                Expression = normalizedExpr,
                NormalizedExpression = flattenedExpr
            };
        }
    }

    private static MyColumn NormalizeColumn(MyColumn column, MyNormalizer normalizer)
    {
        ColumnOption.ColumnDefault? normalizedDefault = column.Default switch
        {
            ColumnOption.ColumnDefault.DefaultExpression de => de with { Expression = normalizer.NormalizeExpressionNoFlatten(de.Expression), NormalizedExpression = normalizer.NormalizeExpression(de.Expression) },
            _ => column.Default
        };
        ColumnOption.Generated? normalizedGenerated = column.Generated switch
        {
            ColumnOption.Generated.AsExpression ge => ge with { Expression = normalizer.NormalizeExpressionNoFlatten(ge.Expression), NormalizedExpression = normalizer.NormalizeExpression(ge.Expression) },
            _ => column.Generated
        };
        ColumnOption.OnUpdate? normalizedOnUpdate = column.OnUpdate != null
            ? new ColumnOption.OnUpdate(normalizer.NormalizeExpression(column.OnUpdate.Expression))
            : null;
        ColumnOption.CheckConstraint? normalizedCheck = column.Check != null
            ? column.Check with { Expression = normalizer.NormalizeExpressionNoFlatten(column.Check.Expression), NormalizedExpression = normalizer.NormalizeExpression(column.Check.Expression) }
            : null;
        return column with
        {
            Default = normalizedDefault,
            Generated = normalizedGenerated,
            OnUpdate = normalizedOnUpdate,
            Check = normalizedCheck
        };
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Retrieving unapplied refactors for schema '{Schema}'")]
    private static partial void s_logRetrievingUnappliedRefactors(ILogger logger, string schema);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Loading schema '{Schema}' from database.")]
    private static partial void s_loadingSchemaFromDatabase(ILogger logger, string schema);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Loaded definition from database: {LoadedDefinition}")]
    private static partial void s_logLoadedDefinition(ILogger logger, LogDelegateWrapper<MyDefinition> loadedDefinition);
}
