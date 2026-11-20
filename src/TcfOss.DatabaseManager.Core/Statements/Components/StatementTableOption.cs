using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementTableOption() : IWriteSql, IHaveDuplicateCheckKey
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract string DuplicateCheckKey { get; }
}
