using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public record PgTrigger(ObjectIdentifier Name, ObjectIdentifier OnTable, TriggerTime TriggerTime,
    SqlValueList<TriggerEvent> Events, ObjectIdentifier FunctionName, SqlValueList<string> Arguments)
    : IDatabaseObject<CreateTrigger>
{
    public ObjectType ObjectType => ObjectType.Trigger;
    public TriggerForEachType ForEachType { get; init; } = TriggerForEachType.Statement;
    public PgTriggerConstraintOptions? ConstraintOptions { get; init; }
    public Identifier? OldTransitionTable { get; init; }
    public Identifier? NewTransitionTable { get; init; }
    public Expression? When { get; init; }

    public CreateTrigger ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        SqlValueList<PgTriggerTransitionRelation> relations = [];
        if (OldTransitionTable != null)
        {
            relations.Add(new PgTriggerTransitionRelation(false, OldTransitionTable, true));
        }
        if (NewTransitionTable != null)
        {
            relations.Add(new PgTriggerTransitionRelation(true, NewTransitionTable, true));
        }
        var body = new PgTriggerExecute(FunctionName.ToObjectName(includeSchema ? 2 : 1),
            [.. Arguments.Select(argument => (PgStatementTriggerArgument)new PgStatementTriggerArgument.Literal(
                new Value.SingleQuotedString(argument.Replace("'", "''", StringComparison.Ordinal))))], false);
        return new CreateTrigger(Name.ToObjectName(1), TriggerTime, Events, OnTable.ToObjectName(includeSchema ? 2 : 1), body)
        {
            ExecutionQuantifier = new TriggerExecutionQuantifier.ForEach(ForEachType),
            PgOptions = new PgStatementTriggerOptions
            {
                EventsUseOr = Events.Count > 1,
                ConstraintOptions = ConstraintOptions is { } constraint
                    ? new PgTriggerConstraint(constraint.FromTable?.ToObjectName(includeSchema ? 2 : 1), constraint.Deferrable, constraint.InitiallyDeferred)
                    : null,
                TransitionRelations = relations.Count == 0 ? null : relations,
                When = When
            }
        };
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.CreateTriggerTable))
        {
            ItemRef? table = context.CreateObjectRef(OnTable.ToObjectName(2));
            if (table != null)
            {
                yield return table;
            }
            if (ConstraintOptions?.FromTable is { } fromTable)
            {
                ItemRef? referenced = context.CreateObjectRef(fromTable.ToObjectName(2));
                if (referenced != null)
                {
                    yield return referenced;
                }
            }
        }
        ItemRef? function = context.CreateFunctionOrProcRef(FunctionName.ToObjectName(2));
        if (function != null)
        {
            yield return function;
        }
        if (When != null)
        {
            foreach (ItemRef.ObjectNameRef reference in When.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>())
            {
                yield return reference;
            }
        }
    }
}
