using Microsoft.EntityFrameworkCore;
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
            List<CreateProcedureEntity> procs = await _context.Database
                .SqlQueryRaw<CreateProcedureEntity>($"SHOW CREATE PROCEDURE `{safeSchema}`.`{safeName}`")
                .ToListAsync(cancellationToken);

            if (procs.Count == 0)
            {
                throw new InvalidOperationException($"No procedure found with name {procedureId}");
            }
            else if (procs.Count > 1)
            {
                throw new InvalidOperationException($"Multiple procedures found with name {procedureId}");
            }

            return procs[0];
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
            List<CreateViewEntity> views = await _context.Database
                .SqlQueryRaw<CreateViewEntity>($"SHOW CREATE VIEW `{safeSchema}`.`{safeName}`")
                .ToListAsync(cancellationToken);

            if (views.Count == 0)
            {
                throw new InvalidOperationException($"No view found with name {viewId}");
            }
            else if (views.Count > 1)
            {
                throw new InvalidOperationException($"Multiple views found with name {viewId}");
            }

            return views[0];
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
