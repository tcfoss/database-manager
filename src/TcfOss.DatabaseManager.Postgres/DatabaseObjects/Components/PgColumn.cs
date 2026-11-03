using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgColumn(ColumnIdentifier Name, DataType DataType)
{
    public ColumnOption.Nullability? Nullability { get; init; }
    public ColumnOption.ColumnDefault? Default { get; init; }

    public StatementColumn.Typed ToStatementColumn(DifferFormatManager? manager = null)
    {
        SqlValueList<StatementColumnOption> options = [];
        if (Nullability is ColumnOption.Nullability.Null nullable)
        {
            options.Add(new StatementColumnOption.Nullability.Null(nullable.Name));
        }
        else if (Nullability is ColumnOption.Nullability.NotNull notNull)
        {
            options.Add(new StatementColumnOption.Nullability.NotNull(notNull.Name));
        }

        if (Default is ColumnOption.ColumnDefault.DefaultValue value)
        {
            options.Add(new StatementColumnOption.Default.DefaultValue(value.Value, value.Name));
        }
        else if (Default is ColumnOption.ColumnDefault.DefaultExpression expression)
        {
            options.Add(new StatementColumnOption.Default.DefaultExpression(expression.Expression, expression.Name));
        }

        return new StatementColumn.Typed(Name.ToSimpleIdentifier(),
            manager != null ? DataType.ToDataTypeForTable(manager) : DataType, options);
    }
}
