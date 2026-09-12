using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

public record MyColumn(ColumnIdentifier Name, DataType DataType)
    : IColumn
{
    public ColumnOption.Nullability? Nullability { get; init; }
    public ColumnOption.ColumnDefault? Default { get; init; }
    public ColumnOption.Generated? Generated { get; init; }
    public ColumnOption.OnUpdate? OnUpdate { get; init; }
    public bool AutoIncrement { get; init; }

    public ColumnOption.Unique? Unique { get; init; }
    public ColumnOption.CheckConstraint? Check { get; init; }

    public Comment? Comment { get; init; }

    public bool Equals(IColumn? other) => Equals(other as MyColumn);

    public StatementColumn ToStatementColumn(DifferFormatManager manager)
    {
        var options = new SqlValueList<StatementColumnOption>();

        if (Generated == null)
        {
            // If column is generated, these other options are syntax errors.
            bool isNotNull = false;
            if (Nullability is ColumnOption.Nullability.Null)
            {
                options.Add(new StatementColumnOption.Nullability.Null());
            }
            else if (Nullability is ColumnOption.Nullability.NotNull)
            {
                isNotNull = true;
                options.Add(new StatementColumnOption.Nullability.NotNull());
            }

            if (Default is ColumnOption.ColumnDefault.DefaultValue dv && !(isNotNull && dv.Value is Value.Null))
            {
                options.Add(new StatementColumnOption.Default.DefaultValue(dv.Value, dv.Name));
            }
            else if (Default is ColumnOption.ColumnDefault.DefaultExpression de)
            {
                options.Add(new StatementColumnOption.Default.DefaultExpression(de.Expression, de.Name));
            }
            if (OnUpdate != null)
            {
                options.Add(new StatementColumnOption.OnUpdate(OnUpdate.Expression));
            }

            if (AutoIncrement)
            {
                options.Add(new StatementColumnOption.AutoIncrement());
            }
        }

        if (Generated is ColumnOption.Generated.AsExpression ge)
        {
            options.Add(new StatementColumnOption.Generated.AsExpression(ge.Expression, ge.Mode, true, true, false));
        }

        if (Check != null)
        {
            options.Add(new StatementColumnOption.CheckConstraint(Check.Expression, Check.Name));
        }

        if (Unique != null)
        {
            options.Add(new StatementColumnOption.Unique(Unique.Name));
        }

        if (Comment != null)
        {
            options.Add(new StatementColumnOption.ColumnComment(new Comment(Comment.Text)));
        }

        return new StatementColumn(new Identifier(Name.Name, Name.QuoteStyle), DataType.ToDataTypeForTable(manager), options);
    }
}
