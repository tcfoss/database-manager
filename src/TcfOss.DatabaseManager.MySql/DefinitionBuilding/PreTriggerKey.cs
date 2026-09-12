using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public readonly record struct PreTriggerKey(ObjectIdentifier OnTable, TriggerTime TriggerTime, TriggerEvent TriggerEvent);
