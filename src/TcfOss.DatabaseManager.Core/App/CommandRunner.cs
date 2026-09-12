using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.App;

public class CommandRunner<T>(T config, IWriteFiles fileWriter, IFormatSqlFiles sqlFormatter) : IRunCommands
    where T : ConfigBase
{
    protected T Config { get; } = config;

    protected IWriteFiles FileWriter { get; } = fileWriter;
    private IFormatSqlFiles SqlFormatter { get; } = sqlFormatter;

    public virtual void ParseFiles(string[] files, string outputPattern, bool includeMeta = false, FileExistsAction fileExistsAction = FileExistsAction.Skip)
    {
        FileParser fileParser = CreateFileParser();
        var serializationSettings = new SerializationSettings
        {
            OmitMetaData = !includeMeta,
            OmitPreNonSql = !includeMeta,
            OmitRawText = true
        };

        foreach (string f in files)
        {
            SqlValueList<Statement> statements = fileParser.ParseFile(f, out _);

            string fileName = Path.GetFileNameWithoutExtension(f);
            string outputFile = outputPattern.Replace("{fileName}", fileName);
            FileWriter.WriteTextToFile(outputFile, Serialization.ToJson(statements, serializationSettings), fileExistsAction);
        }
    }

    public virtual void ParseDefinition(string outputPath, bool relaxed = false, bool includeMeta = false, bool includeRawText = false, bool includeSourceRef = false, FileExistsAction fileExistsAction = FileExistsAction.Error)
    {
        throw new CommandException.SpecificDialectRequired("parse-definition");
    }

    public virtual void FormatSql(string[] files, string? outputPattern, bool noBackup = false, bool relaxed = true, FileExistsAction fileExistsAction = FileExistsAction.Overwrite, string? definitionFile = null)
    {
        SqlFormatter.FormatSql(files, outputPattern, noBackup, relaxed, fileExistsAction, definitionFile);
    }

    public virtual Task DownloadSchemaAsync()
    {
        throw new CommandException.SpecificDialectRequired("download-schema");
    }

    public virtual Task ComputeChangesAsync(string? outputPath, FileExistsAction fileExistsAction = FileExistsAction.Error)
    {
        throw new CommandException.SpecificDialectRequired("compute-changes");
    }

    public void ValidateConfiguration(bool printConfig, TextWriter? output = null)
    {
        output ??= Console.Out;
        if (printConfig)
        {
            string configJson = Serialization.ToYaml(Config);
            output.WriteLine(configJson);
        }
        output.WriteLine();
        output.WriteLine($"Database connection available: {Config.DatabaseAvailable}");
    }

    protected virtual FileParser CreateFileParser()
    {
        return new FileParser();
    }
}
