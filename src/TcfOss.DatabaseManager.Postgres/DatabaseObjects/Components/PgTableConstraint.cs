using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public abstract record PgTableConstraint(Identifier? Name = null)
{
    public abstract StatementTableConstraint ToStatementConstraint();

    public record Unique(SqlValueList<Identifier> Columns, Identifier Name) : PgTableConstraint(Name)
    {
        public IncludedColumns? IncludedColumns { get; init; }

        public override StatementTableConstraint.UniqueConstraint ToStatementConstraint()
        {
            return new StatementTableConstraint.UniqueConstraint([.. Columns.Select(column => new KeyPart.Column(column))], Name)
            {
                IncludedColumns = IncludedColumns
            };
        }
    }

    public record ForeignKey(SqlValueList<Identifier> Columns, ObjectIdentifier ReferencedTable,
        SqlValueList<Identifier> ReferencedColumns, Identifier? Name = null) : PgTableConstraint(Name)
    {
        public ReferentialAction? OnDelete { get; init; }
        public ReferentialAction? OnUpdate { get; init; }

        public override StatementTableConstraint.ForeignKey ToStatementConstraint()
        {
            return new StatementTableConstraint.ForeignKey([.. Columns], ReferencedTable.ToObjectName(2), [.. ReferencedColumns], Name)
            {
                OnDelete = OnDelete,
                OnUpdate = OnUpdate
            };
        }
    }

    public record Check(Expression Expression, Identifier? Name = null) : PgTableConstraint(Name)
    {
        public override StatementTableConstraint.Check ToStatementConstraint()
        {
            return new StatementTableConstraint.Check(Expression, Name);
        }
    }
}
