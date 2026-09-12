namespace TcfOss.DatabaseManager.Core.IO;

// Interface also provided for library usage
// ReSharper disable UnusedMemberInSuper.Global
public interface IWriteFiles
{
    public void RenameExistingFile(string filePath);

    public void WriteTextToFile(string filePath, string text, FileExistsAction fileExistsAction);

    public StreamWriter? GetFileStreamWriter(string filePath, FileExistsAction fileExistsAction);

    public string? GetNextBackupExtension(string directory, string fileName, bool alreadyIncludesBakExtension = false);
}
