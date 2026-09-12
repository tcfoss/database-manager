using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

/// <summary>
/// Provider-specific table reference hint (e.g. T-SQL <c>WITH (NOLOCK)</c>,
/// <c>WITH (INDEX(idx1))</c>). The outer <c>WITH (...)</c> wrapper is
/// emitted by the containing <see cref="TableFactor.Table"/>; concrete
/// subclasses are responsible only for writing their own contents.
/// </summary>
public abstract record TableHint : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }
}
