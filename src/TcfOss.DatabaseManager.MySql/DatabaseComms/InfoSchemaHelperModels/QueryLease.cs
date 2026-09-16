using Microsoft.EntityFrameworkCore;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;

public sealed class QueryLease<TContext>(TContext context, SemaphoreSlim queryGate) : IAsyncDisposable
    where TContext : DbContext
{
    public TContext Context { get; } = context;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Context.DisposeAsync();
        }
        finally
        {
            queryGate.Release();
        }
    }
}
