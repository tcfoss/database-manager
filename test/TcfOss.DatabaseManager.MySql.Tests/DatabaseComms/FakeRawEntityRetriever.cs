using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Tests.Resources;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

public class FakeRawEntityRetriever : IRetrieveRawEntities
{
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
        throw new KeyNotFoundException($"No view found with name {viewId}");
    }
}
