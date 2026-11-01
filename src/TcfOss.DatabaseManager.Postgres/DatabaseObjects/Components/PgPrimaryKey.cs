using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgPrimaryKey(SqlValueList<Identifier> Columns, Identifier? Name = null)
{
    public StatementTableConstraint.PrimaryKey ToStatementConstraint()
    {
        return new StatementTableConstraint.PrimaryKey([.. Columns.Select(column => new KeyPart.Column(column))], Name);
    }
}
