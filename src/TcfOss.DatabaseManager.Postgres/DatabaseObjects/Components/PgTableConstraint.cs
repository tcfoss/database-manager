using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public abstract record PgTableConstraint(Identifier Name)
{
    public PgConstraintOptions Options { get; init; } = new();

    public abstract StatementTableConstraint ToStatementConstraint(bool removeOptionalElements);

    public record Unique(SqlValueList<Identifier> Columns, Identifier Name) : PgTableConstraint(Name)
    {
        public IncludedColumns? IncludedColumns { get; init; }
        public bool NullsDistinct { get; init; } = true;
        public PgBackingIndex BackingIndex { get; init; } = new();

        public override StatementTableConstraint.UniqueConstraint ToStatementConstraint(bool removeOptionalElements)
        {
            return new StatementTableConstraint.UniqueConstraint([.. Columns.Select(column => new StatementKeyPart.Column(column))], Name)
            {
                IncludedColumns = IncludedColumns,
                IndexParameters = BackingIndex.ToStatementIndexParameters(),
                NullsDistinct = NullsDistinct ? null : false,
                PgOptions = Options.ToStatementOptions()
            };
        }
    }

    public record ForeignKey(SqlValueList<Identifier> Columns, ObjectIdentifier ReferencedTable,
        SqlValueList<Identifier> ReferencedColumns, Identifier Name) : PgTableConstraint(Name)
    {
        public ReferentialAction? OnDelete { get; init; }
        public ReferentialAction? OnUpdate { get; init; }

        public override StatementTableConstraint.ForeignKey ToStatementConstraint(bool removeOptionalElements)
        {
            return new StatementTableConstraint.ForeignKey([.. Columns], ReferencedTable.ToObjectName(2), [.. ReferencedColumns], Name)
            {
                OnDelete = OnDelete,
                OnUpdate = OnUpdate,
                PgOptions = Options.ToStatementOptions()
            };
        }
    }

    public record Check(Expression Expression, Identifier Name) : PgTableConstraint(Name)
    {
        public Expression NormalizedExpression { get; init; } = Expression;

        public virtual bool Equals(Check? other)
        {
            return other is not null && base.Equals(other)
                && NormalizedExpression == other.NormalizedExpression;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), NormalizedExpression);
        }

        public override StatementTableConstraint.Check ToStatementConstraint(bool removeOptionalElements)
        {
            return new StatementTableConstraint.Check(Expression, Name)
            {
                PgOptions = Options.ToStatementOptions()
            };
        }
    }

    public record Exclusion(SqlValueList<Exclusion.Element> Elements, Identifier Name) : PgTableConstraint(Name)
    {
        public PgBackingIndex BackingIndex { get; init; } = new();
        public IndexMethod Method { get; init; } = IndexMethod.Default;
        public IncludedColumns? IncludedColumns { get; init; }
        public Expression? Predicate { get; init; }
        public Expression? NormalizedPredicate { get; init; }

        public virtual bool Equals(Exclusion? other)
        {
            return other is not null && base.Equals(other)
                && Elements.Equals(other.Elements) && BackingIndex == other.BackingIndex
                && Method == other.Method && IncludedColumns == other.IncludedColumns
                && (NormalizedPredicate ?? Predicate) == (other.NormalizedPredicate ?? other.Predicate);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Elements, BackingIndex, Method, IncludedColumns, NormalizedPredicate ?? Predicate);
        }

        public override StatementTableConstraint.PgExclusion ToStatementConstraint(bool removeOptionalElements)
        {
            return new StatementTableConstraint.PgExclusion(
                Elements.Map(element => new PgExclusionElement(element.Key.ToStatementKeyPart(removeOptionalElements), element.Operator)), Name)
            {
                Method = Method,
                IncludedColumns = IncludedColumns,
                Predicate = Predicate,
                IndexParameters = BackingIndex.ToStatementIndexParameters(),
                PgOptions = Options.ToStatementOptions()
            };
        }

        public record Element(PgKeyPart Key, PgOperatorReference Operator);
    }
}
