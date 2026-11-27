using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Tests.Resources;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

public class FakeRawEntityRetriever : IRetrieveRawEntities
{
    public Dictionary<(string Schema, string Name), string?> FunctionDefinitions { get; } = [];
    public Dictionary<(string Schema, string Name), string?> TriggerDefinitions { get; } = [];
    public List<IReadOnlyCollection<ObjectIdentifier>> FunctionBatches { get; } = [];
    public List<IReadOnlyCollection<ObjectIdentifier>> TriggerBatches { get; } = [];

    public Task<Dictionary<ObjectIdentifier, CreateFunctionEntity>> GetCreateFunctionsAsync(IReadOnlyCollection<ObjectIdentifier> functionIds, CancellationToken cancellationToken = default)
    {
        FunctionBatches.Add(functionIds);
        var results = new Dictionary<ObjectIdentifier, CreateFunctionEntity>();
        foreach (ObjectIdentifier id in functionIds)
        {
            if (!FunctionDefinitions.TryGetValue((id.Schema.Name, id.Name), out string? text))
            {
                var routine = MySqlContextTestData.GetRoutineEntities().Single(r => r.RoutineSchema == id.Schema.Name && r.RoutineName == id.Name && r.RoutineType == "FUNCTION");
                text = $"CREATE FUNCTION `{id.Name}`() RETURNS {routine.DtdIdentifier} {routine.RoutineDefinition}";
            }
            results.Add(id, new CreateFunctionEntity { Function = id.Name, CreateFunction = text });
        }
        return Task.FromResult(results);
    }

    public Task<Dictionary<ObjectIdentifier, CreateTriggerEntity>> GetCreateTriggersAsync(IReadOnlyCollection<ObjectIdentifier> triggerIds, CancellationToken cancellationToken = default)
    {
        TriggerBatches.Add(triggerIds);
        var results = new Dictionary<ObjectIdentifier, CreateTriggerEntity>();
        foreach (ObjectIdentifier id in triggerIds)
        {
            if (!TriggerDefinitions.TryGetValue((id.Schema.Name, id.Name), out string? text))
            {
                var trigger = MySqlContextTestData.GetTriggerEntities().Single(t => t.TriggerSchema == id.Schema.Name && t.TriggerName == id.Name);
                text = $"CREATE TRIGGER `{id.Name}` {trigger.ActionTiming} {trigger.EventManipulation} ON `{trigger.EventObjectTable}` FOR EACH ROW {trigger.ActionStatement}";
            }
            results.Add(id, new CreateTriggerEntity { Trigger = id.Name, CreateTrigger = text });
        }
        return Task.FromResult(results);
    }

    public Task<CreateProcedureEntity> GetCreateProcedureAsync(ObjectIdentifier procedureId, CancellationToken cancellationToken = default)
    {
        if (procedureId.Schema.Name == "library_activity" && procedureId.Name == "search_available_books")
        {
            return Task.FromResult(new CreateProcedureEntity
            {
                Procedure = procedureId,
                CreateProcedure = TestText.RawCreateProcedure_LibraryActivity_SearchAvailableBooks
            });
        }
        if (procedureId.Schema.Name == "extra_test" && procedureId.Name == "proc_no_params")
        {
            return Task.FromResult(new CreateProcedureEntity
            {
                Procedure = procedureId,
                CreateProcedure = TestText.RawCreateProcedure_ExtraTest_ProcNoParams
            });
        }
        throw new KeyNotFoundException($"No procedure found with name {procedureId}");
    }

    public Task<CreateViewEntity> GetCreateViewAsync(ObjectIdentifier viewId, CancellationToken cancellationToken = default)
    {
        // "available_books", new SchemaIdentifier("library_activity"
        if (viewId.Schema.Name == "library_activity" && viewId.Name == "available_books")
        {
            return Task.FromResult(new CreateViewEntity
            {
                View = viewId,
                CreateView = TestText.RawCreateView_LibraryActivity_AvailableBooks
            });
        }
        if (viewId.Schema.Name == "extra_test" && viewId.Name == "view_with_options")
        {
            return Task.FromResult(new CreateViewEntity
            {
                View = viewId,
                CreateView = TestText.RawCreateView_ExtraTest_ViewWithOptions
            });
        }
        throw new KeyNotFoundException($"No view found with name {viewId}");
    }
}
