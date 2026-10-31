using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public abstract record PgDomainConstraint(Identifier? Name = null)
{
    public record Nullability(bool NotNull, Identifier? Name = null) : PgDomainConstraint(Name);

    public record Check(Expression Expression, Identifier? Name = null) : PgDomainConstraint(Name);
}
