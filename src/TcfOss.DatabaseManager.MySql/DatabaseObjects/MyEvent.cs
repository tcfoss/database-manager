using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects;

public record MyEvent(ObjectIdentifier Name, EventSchedule Schedule, Statement Body)
    : IDatabaseObject
{
    public ObjectType ObjectType => ObjectType.Event;

    public required Definer Definer { get; init; }
    public required bool OnCompletionPreserve { get; init; }
    public required EventEnabledStatus EventEnabledStatus { get; init; }
    public Comment? Comment { get; init; }
    public string? RawBodyText { get; init; }

    public virtual bool Equals(MyEvent? other)
    {
        if (other == null)
        {
            return false;
        }

        // `RawBodyText` is excluded from the equality contract.
        return Name == other.Name
            && Schedule == other.Schedule
            && Body == other.Body
            && Definer == other.Definer
            && OnCompletionPreserve == other.OnCompletionPreserve
            && EventEnabledStatus == other.EventEnabledStatus
            && Comment == other.Comment;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Schedule, Body, Definer, OnCompletionPreserve, EventEnabledStatus, Comment);
    }

    public CreateEvent ToCreateStatement(bool includeSchema)
    {
        return new CreateEvent(Name.ToObjectName(includeSchema ? 2 : 1), Schedule, Body)
        {
            Definer = Definer,
            OnCompletionPreserve = OnCompletionPreserve,
            EnabledStatus = EventEnabledStatus,
            Comment = Comment,
            Meta = new MetaData { RawText = RawBodyText }
        };
    }

    Statement IDatabaseObject.ToCreateStatement(bool includeSchema, DifferFormatManager? manager) => ToCreateStatement(includeSchema);

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
