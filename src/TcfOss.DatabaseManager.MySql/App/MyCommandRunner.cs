using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.App;

public partial class MyCommandRunner(
    MyConfig config,
    IParser parser,
    ILexer lexer,
    IFormatSqlFiles sqlFormatter,
    ILoadFsDefinition<MyDefinition> fsLoader,
    IDownloadSchema schemaDownloader,
    IWriteFiles fileWriter,
    IComputeChanges changeComputer,
    SourceManager sourceManager,
    ILogger<MyCommandRunner> logger) : CommandRunner<MyConfig>(config, fileWriter, sqlFormatter)
{
    private readonly IParser _parser = parser;
    private readonly ILexer _lexer = lexer;
    private readonly ILoadFsDefinition<MyDefinition> _fsLoader = fsLoader;
    private readonly IDownloadSchema _schemaDownloader = schemaDownloader;
    private readonly IComputeChanges _changeComputer = changeComputer;
    private readonly SourceManager _sourceManager = sourceManager;
    private readonly ILogger<MyCommandRunner> _logger = logger;

    // void ParseFiles: base implementation will do.

    public override void ParseDefinition(string outputPath, bool relaxed = false, bool includeMeta = false, bool includeRawText = false, bool includeSourceRef = false, FileExistsAction fileExistsAction = FileExistsAction.Error)
    {
        if (Config == null)
        {
            throw new CommandException.ConfigurationRequired("parse-definition");
        }

        s_logBeginParseDefinition(_logger, Config.ProjectDirectory);
        MyDefinition definition = _fsLoader.LoadDefinition(relaxed: relaxed);
        var parsedDefinition = new ParsedDefinition<MyDefinition>
        {
            Version = typeof(MyDefinition).Assembly.GetName().Version ?? new Version(0, 0, 0, 0),
            ParsedDate = DateTime.UtcNow,
            Definition = definition,
            Sources = _sourceManager,
        };
        var serializationSettings = new SerializationSettings
        {
            OmitMetaData = !includeMeta,
            OmitPreNonSql = !includeMeta,
            OmitRawText = !includeRawText,
            OmitSourceRef = !includeSourceRef
        };
        string outputText = Serialization.ToJson(parsedDefinition, serializationSettings);

        string outputFullPath = Path.GetFullPath(outputPath);
        FileWriter.WriteTextToFile(outputFullPath, outputText, fileExistsAction);
        s_logCompleteParseDefinition(_logger, outputFullPath);
    }

    public override async Task DownloadSchemaAsync()
    {
        if (Config == null)
        {
            throw new CommandException.ConfigurationRequired("download-schema");
        }

        s_logBeginDownloadSchema(_logger);
        await _schemaDownloader.ExecuteAsync();
        s_logCompleteDownloadSchema(_logger);
    }

    public override async Task ComputeChangesAsync(string? outputPath, FileExistsAction fileExistsAction = FileExistsAction.Error)
    {
        if (Config == null)
        {
            throw new CommandException.ConfigurationRequired("compute-changes");
        }

        s_logBeginComputeChanges(_logger);
        await _changeComputer.ExecuteAsync(outputPath, fileExistsAction);
        s_logCompleteComputeChanges(_logger);
    }

    protected override FileParser CreateFileParser()
    {
        return new FileParser(_lexer, _parser);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Parsing MySQL definition from file system in '{RootDirectory}'.")]
    private static partial void s_logBeginParseDefinition(ILogger logger, string rootDirectory);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "MySQL definition parsed and saved to '{OutputFullPath}'.")]
    private static partial void s_logCompleteParseDefinition(ILogger logger, string outputFullPath);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Downloading MySQL schema...")]
    private static partial void s_logBeginDownloadSchema(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "MySQL schema download completed.")]
    private static partial void s_logCompleteDownloadSchema(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Computing changes for MySQL schema...")]
    private static partial void s_logBeginComputeChanges(ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "MySQL schema changes computed.")]
    private static partial void s_logCompleteComputeChanges(ILogger logger);
}
