using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.App;

public interface IFormatSqlFiles
{
    public void FormatSql(string[] files, string? outputPattern, bool noBackup = false, bool relaxed = true, FileExistsAction fileExistsAction = FileExistsAction.Overwrite, string? definitionFile = null);
}
