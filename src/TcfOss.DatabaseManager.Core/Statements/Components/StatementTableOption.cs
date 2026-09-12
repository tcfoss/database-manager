using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementTableOption() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract string? DuplicateCheckKey { get; }
}
