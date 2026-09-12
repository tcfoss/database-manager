using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct RoutineInfo()
{
    public required Definer Definer { get; init; }
    public required SecurityContext SecurityContext { get; init; }
    public required SqlDataRelation SqlDataRelation { get; init; }
    public Comment? Comment { get; init; }
    public bool IsDeterministic { get; init; }
}
