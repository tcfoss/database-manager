using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public abstract record PgTableConstraint(Identifier? Name = null)
{
    public PgConstraintOptions Options { get; init; } = new();

    public abstract StatementTableConstraint ToStatementConstraint(bool removeOptionalElements);

    public record Unique(SqlValueList<Identifier> Columns, Identifier Name) : PgTableConstraint(Name)
    {
        public IncludedColumns? IncludedColumns { get; init; }
        public bool NullsDistinct { get; init; } = true;
        public PgIndex? BackingIndex { get; init; }

        public override StatementTableConstraint.UniqueConstraint ToStatementConstraint(bool removeOptionalElements)
        {
            return new StatementTableConstraint.UniqueConstraint([.. Columns.Select(column => new StatementKeyPart.Column(column))], Name)
            {
                IncludedColumns = IncludedColumns,
                IndexParameters = BackingIndex?.Tablespace is { } tablespace ? new PgConstraintIndexParameters(null, tablespace) : null,
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
        public required PgIndex BackingIndex { get; init; }

        public override StatementTableConstraint.PgExclusion ToStatementConstraint(bool removeOptionalElements)
        {
            SqlValueList<StatementIndexOption>? indexOptions = BackingIndex.ToStatementIndexOptions();
            return new StatementTableConstraint.PgExclusion(
                Elements.Map(element => new PgExclusionElement(element.Key.ToStatementKeyPart(removeOptionalElements), element.Operator)), Name)
            {
                Method = BackingIndex.Method,
                IncludedColumns = BackingIndex.IncludedColumns,
                Predicate = BackingIndex.Predicate,
                IndexParameters = indexOptions != null || BackingIndex.Tablespace != null
                    ? new PgConstraintIndexParameters(indexOptions, BackingIndex.Tablespace) : null,
                PgOptions = Options.ToStatementOptions()
            };
        }

        public record Element(PgKeyPart Key, PgOperatorReference Operator);
    }
}
