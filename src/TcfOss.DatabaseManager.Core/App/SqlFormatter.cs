using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.App;

public partial class SqlFormatter<T>(
    T config,
    IWriteFiles fileWriter,
    IParseText textParser,
    IFunctionNameProvider functionNameProvider,
    ILogger<SqlFormatter<T>> logger) : IFormatSqlFiles
    where T : ConfigBase
{
    protected T Config { get; } = config;
    private IWriteFiles FileWriter { get; } = fileWriter;
    private IParseText TextParser { get; } = textParser;
    private IFunctionNameProvider FunctionNameProvider { get; } = functionNameProvider;
    private readonly ILogger<SqlFormatter<T>> _logger = logger;

    public virtual void FormatSql(string[] files, string? outputPattern, bool noBackup = false, bool relaxed = true, FileExistsAction fileExistsAction = FileExistsAction.Overwrite, string? definitionFile = null)
    {
        PseudoTableSet? pseudoTableSet = GetPseudoTableSet(relaxed, definitionFile);

        foreach (string f in files)
        {
            using var formatter = new Formatter(Config, TextParser, FunctionNameProvider);
            formatter.PseudoTables = pseudoTableSet?.CloneExternalOnly();

            string fullPath = Path.GetFullPath(f);
            string fileDir = Path.GetDirectoryName(fullPath) ?? "";

            string rawText = File.ReadAllText(fullPath);
            string formatted = formatter.GetFormatted(rawText);

            string outputPath;
            if (outputPattern != null)
            {
                string baseName = Path.GetFileNameWithoutExtension(f);
                outputPath = Path.Combine(fileDir, outputPattern.Replace("{fileName}", baseName));
            }
            else
            {
                outputPath = fullPath;
                if (noBackup)
                {
                    s_logOverwritingFileWithoutBackup(_logger, outputPath, rawText, null);
                    File.Delete(outputPath);
                }
                else if (File.Exists(outputPath))
                {
                    FileWriter.RenameExistingFile(outputPath);
                }
            }

            FileWriter.WriteTextToFile(outputPath, formatted, fileExistsAction);
        }
    }

    protected virtual PseudoTableSet? GetPseudoTableSet(bool relaxed, string? definitionFile = null)
    {
        return null;
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Overwriting file without backup: {FilePath}\n{Body}")]
    private static partial void s_logOverwritingFileWithoutBackup(ILogger logger, string filePath, string body, Exception? ex);
}
