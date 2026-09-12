using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.App;

public interface IRunCommands
{
    public void ParseFiles(string[] files, string outputPattern, bool includeMeta = false, FileExistsAction fileExistsAction = FileExistsAction.Skip);
    public void ParseDefinition(string outputPath, bool relaxed = false, bool includeMeta = false, bool includeRawText = false, bool includeSourceRef = false, FileExistsAction fileExistsAction = FileExistsAction.Error);
    public void FormatSql(string[] files, string? outputPattern, bool noBackup = false, bool relaxed = true, FileExistsAction fileExistsAction = FileExistsAction.Overwrite, string? definitionFile = null);
    public Task DownloadSchemaAsync();
    public Task ComputeChangesAsync(string? outputPath, FileExistsAction fileExistsAction = FileExistsAction.Error);
    public void ValidateConfiguration(bool printConfig, TextWriter? output = null);
}
