using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgPrimaryKey(SqlValueList<Identifier> Columns, Identifier Name)
{
    public PgConstraintOptions Options { get; init; } = new();
    public IncludedColumns? IncludedColumns { get; init; }
    public PgBackingIndex BackingIndex { get; init; } = new();

    public StatementTableConstraint.PrimaryKey ToStatementConstraint()
    {
        return new StatementTableConstraint.PrimaryKey([.. Columns.Select(column => new StatementKeyPart.Column(column))], Name)
        {
            IncludedColumns = IncludedColumns,
            IndexParameters = BackingIndex.ToStatementIndexParameters(),
            PgOptions = Options.ToStatementOptions()
        };
    }
}
