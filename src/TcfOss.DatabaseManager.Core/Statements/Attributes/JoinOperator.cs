using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

public abstract record JoinOperator()
{
    public abstract string JoinText { get; }
    public abstract record ConstrainedJoinOperator(JoinConstraint JoinConstraint) : JoinOperator;

    public record Inner(JoinConstraint JoinConstraint) : ConstrainedJoinOperator(JoinConstraint)
    {
        public override string JoinText => "INNER JOIN";
    }

    public record LeftOuter(JoinConstraint JoinConstraint) : ConstrainedJoinOperator(JoinConstraint)
    {
        public override string JoinText => "LEFT OUTER JOIN";
    }

    public record RightOuter(JoinConstraint JoinConstraint) : ConstrainedJoinOperator(JoinConstraint)
    {
        public override string JoinText => "RIGHT OUTER JOIN";
    }

    public record FullOuter(JoinConstraint JoinConstraint) : ConstrainedJoinOperator(JoinConstraint)
    {
        public override string JoinText => "FULL OUTER JOIN";
    }

    public record CrossJoin() : JoinOperator
    {
        public override string JoinText => "CROSS JOIN";
    }

    public record CrossApply() : JoinOperator
    {
        public override string JoinText => "CROSS APPLY";
    }

    public record OuterApply() : JoinOperator
    {
        public override string JoinText => "OUTER APPLY";
    }
}
