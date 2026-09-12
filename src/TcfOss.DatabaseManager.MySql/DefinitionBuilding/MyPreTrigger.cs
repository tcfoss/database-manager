using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public record MyPreTrigger(ObjectIdentifier Name, ObjectIdentifier OnTable, TriggerTime TriggerTime, TriggerEvent TriggerEvent, Statement Body)
    : PreTrigger(Name, OnTable, Body)
{
    public required Definer Definer { get; init; }
    public ObjectIdentifier? AfterTrigger { get; init; }
    public ObjectIdentifier? BeforeTrigger { get; init; }
}
