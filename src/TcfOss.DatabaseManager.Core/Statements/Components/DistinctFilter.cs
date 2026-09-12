using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record DistinctFilter() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public record All() : DistinctFilter
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("ALL");
        }
    }

    public record Distinct() : DistinctFilter
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DISTINCT");
        }
    }

    public record DistinctRow() : DistinctFilter
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DISTINCTROW");
        }
    }

    public record On(SqlValueList<Expression> ColumnNames) : DistinctFilter
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write($"DISTINCT ON ({ColumnNames.ToSqlDelimited()})");
        }
    }
}
