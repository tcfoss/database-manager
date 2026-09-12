using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects;

public record MyTrigger(ObjectIdentifier Name, ObjectIdentifier OnTable, TriggerTime TriggerTime, TriggerEvent TriggerEvent, Statement Body) : Trigger(Name, OnTable, Body)
{
    public required Definer Definer { get; init; }
    public required uint Order { get; init; }

    public virtual bool Equals(MyTrigger? other)
    {
        return base.Equals(other)
            && TriggerTime == other.TriggerTime
            && TriggerEvent == other.TriggerEvent
            && Definer == other.Definer
            && Order == other.Order;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), TriggerTime, TriggerEvent, Definer, Order);
    }

    public override CreateTrigger ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        return new CreateTrigger(Name.ToObjectName(includeSchema ? 2 : 1), TriggerTime, [TriggerEvent], OnTable.ToObjectName(includeSchema ? 2 : 1), Body)
        {
            Definer = Definer,
            ExecutionQuantifier = new TriggerExecutionQuantifier.ForEach(TriggerForEachType.Row, IncludeEach: true),
            Meta = new MetaData { RawText = RawBodyText }
        };
    }
}
