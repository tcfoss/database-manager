using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

public abstract record ColumnOption()
{
    public abstract record WithConstraint(Identifier? Name) : ColumnOption;

    public abstract record Nullability(Identifier? Name) : WithConstraint(Name)
    {
        public record Null(Identifier? Name = null) : Nullability(Name);

        public record NotNull(Identifier? Name = null) : Nullability(Name);
    }

    public abstract record ColumnDefault(Identifier? Name) : WithConstraint(Name)
    {
        public record DefaultValue(Value Value, Identifier? Name = null) : ColumnDefault(Name);

        public record DefaultExpression(Expression Expression, Identifier? Name = null) : ColumnDefault(Name)
        {
            public Expression NormalizedExpression { get; init; } = Expression;

            public virtual bool Equals(DefaultExpression? other)
            {
                return other is not null
                    && Name == other.Name
                    && NormalizedExpression == other.NormalizedExpression;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Name, NormalizedExpression);
            }
        }
    }

    public record CheckConstraint(Expression Expression, Identifier? Name = null) : WithConstraint(Name)
    {
        public Expression NormalizedExpression { get; init; } = Expression;

        public virtual bool Equals(CheckConstraint? other)
        {
            return other is not null
                && Name == other.Name
                && NormalizedExpression == other.NormalizedExpression;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, NormalizedExpression);
        }
    }

    public record Unique(Identifier? Name = null) : WithConstraint(Name);

    public abstract record Generated() : ColumnOption
    {
        public record AsExpression(Expression Expression, GenerationMode Mode) : Generated
        {
            public Expression NormalizedExpression { get; init; } = Expression;

            public virtual bool Equals(AsExpression? other)
            {
                return other is not null
                    && NormalizedExpression == other.NormalizedExpression
                    && Mode == other.Mode;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(NormalizedExpression, Mode);
            }
        }

        // TODO: GENERATED {when} AS IDENTITY
    }

    public record OnUpdate(Expression Expression) : ColumnOption;
}
