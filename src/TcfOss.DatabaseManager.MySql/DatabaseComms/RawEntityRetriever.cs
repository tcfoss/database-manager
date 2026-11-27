using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms;

#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.

public class RawEntityRetriever(DbContext context) : IRetrieveRawEntities, IDisposable
{
    private readonly DbContext _context = context;
    private readonly SemaphoreSlim _queryGate = new(1, 1);

    public async Task<CreateProcedureEntity> GetCreateProcedureAsync(ObjectIdentifier procedureId, CancellationToken cancellationToken = default)
    {
        await _queryGate.WaitAsync(cancellationToken);
        try
        {
            string safeSchema = procedureId.Schema.Name.Replace("`", "``");
            string safeName = procedureId.Name.Replace("`", "``");
            return (await _context.Database
                .SqlQueryRaw<CreateProcedureEntity>($"SHOW CREATE PROCEDURE `{safeSchema}`.`{safeName}`")
                .ToListAsync(cancellationToken)).First();
        }
        finally
        {
            _queryGate.Release();
        }
    }

    public async Task<CreateViewEntity> GetCreateViewAsync(ObjectIdentifier viewId, CancellationToken cancellationToken = default)
    {
        await _queryGate.WaitAsync(cancellationToken);
        try
        {
            string safeSchema = viewId.Schema.Name.Replace("`", "``");
            string safeName = viewId.Name.Replace("`", "``");
            return (await _context.Database
                .SqlQueryRaw<CreateViewEntity>($"SHOW CREATE VIEW `{safeSchema}`.`{safeName}`")
                .ToListAsync(cancellationToken)).First();
        }
        finally
        {
            _queryGate.Release();
        }
    }

    public Task<Dictionary<ObjectIdentifier, CreateTriggerEntity>> GetCreateTriggersAsync(IReadOnlyCollection<ObjectIdentifier> triggerIds, CancellationToken cancellationToken = default)
    {
        return GetCreateStatementsAsync(triggerIds, "TRIGGER", "Trigger", "SQL Original Statement",
            (name, text) => new CreateTriggerEntity { Trigger = name, CreateTrigger = text }, cancellationToken);
    }

    public Task<Dictionary<ObjectIdentifier, CreateFunctionEntity>> GetCreateFunctionsAsync(IReadOnlyCollection<ObjectIdentifier> functionIds, CancellationToken cancellationToken = default)
    {
        return GetCreateStatementsAsync(functionIds, "FUNCTION", "Function", "Create Function",
            (name, text) => new CreateFunctionEntity { Function = name, CreateFunction = text }, cancellationToken);
    }

    private async Task<Dictionary<ObjectIdentifier, T>> GetCreateStatementsAsync<T>(
        IReadOnlyCollection<ObjectIdentifier> objectIds, string objectType, string nameColumn, string createColumn,
        Func<string, string?, T> createEntity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (objectIds.Count == 0)
        {
            return [];
        }

        List<ObjectIdentifier> ids = [.. objectIds];
        StringBuilder sb = new();
        foreach (ObjectIdentifier id in ids)
        {
            string safeSchema = id.Schema.Name.Replace("`", "``");
            string safeName = id.Name.Replace("`", "``");
            sb.AppendLine(CultureInfo.InvariantCulture, $"SHOW CREATE {objectType} `{safeSchema}`.`{safeName}`;");
        }

        await _queryGate.WaitAsync(cancellationToken);
        try
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using DbCommand command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sb.ToString();
                command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

                await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                var results = new Dictionary<ObjectIdentifier, T>();
                for (int i = 0; i < ids.Count; i++)
                {
                    if (i > 0)
                    {
                        await reader.NextResultAsync(cancellationToken);
                    }
                    await reader.ReadAsync(cancellationToken);
                    int nameOrdinal = reader.GetOrdinal(nameColumn);
                    int createOrdinal = reader.GetOrdinal(createColumn);
                    string name = reader.GetString(nameOrdinal);
                    string? text = await reader.IsDBNullAsync(createOrdinal, cancellationToken) ? null : reader.GetString(createOrdinal);
                    results.Add(ids[i], createEntity(name, text));
                }
                return results;
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        finally
        {
            _queryGate.Release();
        }
    }

    public void Dispose()
    {
        _queryGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
