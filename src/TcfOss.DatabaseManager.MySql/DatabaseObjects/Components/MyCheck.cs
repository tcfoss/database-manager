using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

public record MyCheck(Expression Expression, Identifier? ConstraintName) : IHaveOptionalIdentifierName
{
    public Identifier? Name => ConstraintName;
    public Comment? Comment { get; init; }

    public Expression NormalizedExpression { get; init; } = Expression;

    public virtual bool Equals(MyCheck? other)
    {
        return other is not null
            && ConstraintName == other.ConstraintName
            && NormalizedExpression == other.NormalizedExpression;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ConstraintName, NormalizedExpression);
    }

    public StatementTableConstraint.Check ToStatementConstraint()
    {
        return new StatementTableConstraint.Check(Expression, ConstraintName)
        {
            Comment = Comment
        };
    }
}
