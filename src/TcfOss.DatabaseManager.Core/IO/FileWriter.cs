using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.IO;

public partial class FileWriter(ILogger<FileWriter> logger) : IWriteFiles
{
    private readonly ILogger<FileWriter> _logger = logger;
    private static readonly Regex s_existingBakRegex = GetExistingBakRegex();

    public void RenameExistingFile(string filePath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (directory == null || !Directory.Exists(directory))
        {
            s_logCouldNotDetermineDirectory(_logger, filePath, null);
            throw new DirectoryNotFoundException($"Directory for file '{filePath}' not found.");
        }
        string fileName = Path.GetFileName(filePath);

        string? nextBakExtension = GetNextBackupExtension(directory, fileName, alreadyIncludesBakExtension: false);
        if (nextBakExtension == null)
        {
            return;
        }
        string newFileName = $"{fileName}{nextBakExtension}";
        string newFilePath = Path.Combine(directory, newFileName);

        File.Move(filePath, newFilePath);
        s_logRenamedExistingFile(_logger, filePath, newFilePath, null);
    }

    public string? GetNextBackupExtension(string directory, string fileName, bool alreadyIncludesBakExtension = false)
    {
        if (alreadyIncludesBakExtension)
        {
            fileName = fileName.Substring(0, fileName.LastIndexOf(".bak", StringComparison.OrdinalIgnoreCase));
        }

        string filePath = Path.Combine(directory, fileName);
        if (!File.Exists(filePath))
        {
            s_logFileNotFound(_logger, filePath, null);
            return null;
        }

        int? lastBakNumber = Directory.GetFiles(directory, $"{fileName}.bak.*")
            .Select(fn => s_existingBakRegex.Match(Path.GetFileName(fn)))
            .Where(m => m.Success)
            .Select(m => Convert.ToInt32(m.Groups[2].Value, CultureInfo.InvariantCulture))
            .OrderByDescending(n => n)
            .FirstOrDefault();

        int nextBakNumber = (lastBakNumber ?? 0) + 1;

        return $".bak.{nextBakNumber}";
    }

    public void WriteTextToFile(string filePath, string text, FileExistsAction fileExistsAction)
    {
        if (File.Exists(filePath))
        {
            switch (fileExistsAction)
            {
                case FileExistsAction.Skip:
                    s_logFileExistsSkippingWrite(_logger, filePath, null);
                    return;
                case FileExistsAction.Rename:
                    RenameExistingFile(filePath);
                    break;
                case FileExistsAction.Overwrite:
                    // Overwrite the existing file
                    break;
                case FileExistsAction.Error:
                    throw new CommandException.FileExists(filePath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileExistsAction), fileExistsAction, null);
            }
        }

        File.WriteAllText(filePath, text);
    }

    public StreamWriter? GetFileStreamWriter(string filePath, FileExistsAction fileExistsAction)
    {
        if (File.Exists(filePath))
        {
            switch (fileExistsAction)
            {
                case FileExistsAction.Skip:
                    s_logFileExistsSkippingWrite(_logger, filePath, null);
                    return null;
                case FileExistsAction.Rename:
                    RenameExistingFile(filePath);
                    break;
                case FileExistsAction.Overwrite:
                    // Overwrite the existing file
                    break;
                case FileExistsAction.Error:
                    throw new CommandException.FileExists(filePath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileExistsAction), fileExistsAction, null);
            }
        }

        return new StreamWriter(filePath);
    }

    [GeneratedRegex(@"^(.+)\.bak\.(\d+)$", RegexOptions.Compiled)]
    private static partial Regex GetExistingBakRegex();

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Renamed existing file '{FilePath}' to '{NewFilePath}'")]
    private static partial void s_logRenamedExistingFile(ILogger logger, string filePath, string newFilePath, Exception? ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Could not determine directory for file: {FilePath}")]
    private static partial void s_logCouldNotDetermineDirectory(ILogger logger, string filePath, Exception? ex);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "File not found: {FilePath}")]
    private static partial void s_logFileNotFound(ILogger logger, string filePath, Exception? ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "File '{FilePath}' already exists. Skipping write.")]
    private static partial void s_logFileExistsSkippingWrite(ILogger logger, string filePath, Exception? ex);

}
