using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgPrimaryKey(SqlValueList<Identifier> Columns, Identifier Name)
{
    public PgConstraintOptions Options { get; init; } = new();
    public IncludedColumns? IncludedColumns { get; init; }
    public PgIndex? BackingIndex { get; init; }

    public StatementTableConstraint.PrimaryKey ToStatementConstraint()
    {
        return new StatementTableConstraint.PrimaryKey([.. Columns.Select(column => new KeyPart.Column(column))], Name)
        {
            IncludedColumns = IncludedColumns,
            IndexParameters = BackingIndex?.Tablespace is { } tablespace ? new PgConstraintIndexParameters(null, tablespace) : null,
            PgOptions = Options.ToStatementOptions()
        };
    }
}
