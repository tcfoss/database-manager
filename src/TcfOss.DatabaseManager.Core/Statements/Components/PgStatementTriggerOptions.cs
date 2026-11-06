using TcfOss.DatabaseManager.Core.Expressions;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgStatementTriggerOptions
{
    public bool EventsUseOr { get; init; }
    public PgTriggerConstraint? ConstraintOptions { get; init; }
    public SqlValueList<PgTriggerTransitionRelation>? TransitionRelations { get; init; }
    public Expression? When { get; init; }
}
