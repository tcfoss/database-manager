using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public partial class MyFsDefinitionLoader(
    MyConfig config,
    IReadSchemaMapFiles fileLoader,
    IParseText textParser,
    SourceManager sourceManager,
    IFunctionNameProvider functionNameProvider,
    ILogger<MyFsDefinitionLoader> logger,
    ILoggerFactory loggerFactory
) : ILoadFsDefinition<MyDefinition>
{
    private readonly MyConfig _config = config;
    private readonly ILogger<MyFsDefinitionLoader> _logger = logger;
    private readonly IReadSchemaMapFiles _fileLoader = fileLoader;
    private readonly IParseText _textParser = textParser;
    private readonly SourceManager _sourceManager = sourceManager;
    private readonly IFunctionNameProvider _functionNameProvider = functionNameProvider;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public MyDefinition LoadDefinition(bool relaxed)
    {
        MyDefinitionBuilder builder;
        if (relaxed)
        {
            builder = new MyRelaxedDefinitionBuilder(_config, _sourceManager, _functionNameProvider, _loggerFactory.CreateLogger<MyRelaxedDefinitionBuilder>());
        }
        else
        {
            builder = new MyDefinitionBuilder(_config, _sourceManager, _functionNameProvider, _loggerFactory.CreateLogger<MyDefinitionBuilder>());
        }

        var parseResults = new ConcurrentBag<(DefinitionFileInfoWithSchema FileInfo, SqlValueList<Statement> Statements, int SourceId)>();
        var parseError = new ConcurrentQueue<Exception>();

        var cancellationTokenSource = new CancellationTokenSource();
        var parallelOptions = new ParallelOptions { CancellationToken = cancellationTokenSource.Token, MaxDegreeOfParallelism = Environment.ProcessorCount };

        try
        {
            Parallel.ForEach(_fileLoader.GetFilesFlat(), parallelOptions, fileInfo =>
            {
                try
                {
                    parallelOptions.CancellationToken.ThrowIfCancellationRequested();
                    int sourceId = _sourceManager.RegisterFileSource(fileInfo.FullPath);
                    SqlValueList<Statement> stmts = _textParser.ParseText(fileInfo.Text, fileInfo.RelativePath, sourceId);
                    parseResults.Add((fileInfo, stmts, sourceId));
                }
                catch (LexException lexEx)
                {
                    var wrapped = new LexException.WithText(lexEx, fileInfo.RelativePath, fileInfo.Text);
                    parseError.Enqueue(wrapped);
                    cancellationTokenSource.Cancel();
                }
                catch (ParseException parseEx)
                {
                    var wrapped = new ParseException.WithText(parseEx, fileInfo.RelativePath, fileInfo.Text);
                    parseError.Enqueue(wrapped);
                    cancellationTokenSource.Cancel();
                }
                catch (Exception ex)
                {
                    parseError.Enqueue(ex);
                    cancellationTokenSource.Cancel();
                }
            });
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            // fall through to rethrow below with detailed context
        }

        if (parseError.TryDequeue(out Exception? firstError))
        {
            throw firstError;
        }

        // Apply parsed results sequentially to the builder to avoid thread-safety issues
        var filesLoaded = new HashSet<string>();
        foreach ((DefinitionFileInfoWithSchema fileInfo, SqlValueList<Statement> statements, int sourceId) in parseResults)
        {
            if (!filesLoaded.Add(fileInfo.FullPath))
            {
                throw new ConfigurationException.OverlappingSchemas(fileInfo.FullPath);
            }

            s_processingStatementsFromFile(_logger, fileInfo.FullPath, fileInfo.SchemaId);
            builder.ProcessStatements(statements, fileInfo.SchemaId, sourceId);
        }

        s_finishedParsingFiles(_logger);

        MyDefinition definition = builder.ToDefinition();
        s_logParsedDefinition(_logger, new LogDelegateWrapper<MyDefinition>(definition, _logger.IsEnabled(LogLevel.Trace), DefinitionHelpers.SortDefinition));

        return definition;
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Processing statements from file '{FullPath}' for schema '{SchemaId}'...")]
    private static partial void s_processingStatementsFromFile(ILogger logger, string fullPath, string schemaId);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Finished parsing files. Constructing definition...")]
    private static partial void s_finishedParsingFiles(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Parsed definition from files: {ParsedDefinition}")]
    private static partial void s_logParsedDefinition(ILogger logger, LogDelegateWrapper<MyDefinition> parsedDefinition);
}
