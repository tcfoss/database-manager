using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms;

public interface IRetrieveRawEntities
{
    Task<CreateProcedureEntity> GetCreateProcedureAsync(ObjectIdentifier procedureId, CancellationToken cancellationToken = default);
    Task<CreateViewEntity> GetCreateViewAsync(ObjectIdentifier viewId, CancellationToken cancellationToken = default);
    Task<Dictionary<ObjectIdentifier, CreateTriggerEntity>> GetCreateTriggersAsync(IReadOnlyCollection<ObjectIdentifier> triggerIds, CancellationToken cancellationToken = default);
    Task<Dictionary<ObjectIdentifier, CreateFunctionEntity>> GetCreateFunctionsAsync(IReadOnlyCollection<ObjectIdentifier> functionIds, CancellationToken cancellationToken = default);
}
