using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DefinitionMapping;

namespace TcfOss.DatabaseManager.MySql.App;

public partial class MyChangeComputer(
    MyConfig config,
    IWriteFiles fileWriter,
    IServiceScopeFactory scopeFactory,
    IGetUnappliedRefactors unappliedRefactorLoader,
    IGetUnappliedDeployScripts unappliedDeployScriptLoader,
    ILogger<MyChangeComputer> logger
) : IComputeChanges
{
    private readonly MyConfig _config = config;
    private readonly ILogger<MyChangeComputer> _logger = logger;
    private readonly IWriteFiles _fileWriter = fileWriter;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IGetUnappliedRefactors _unappliedRefactorLoader = unappliedRefactorLoader;
    private readonly IGetUnappliedDeployScripts _unappliedDeployScriptLoader = unappliedDeployScriptLoader;

    public async Task ExecuteAsync(string? outputFilePath, FileExistsAction fileExistsAction = FileExistsAction.Error, CancellationToken cancellationToken = default)
    {
        s_computingChanges(_logger, _config.ProjectDirectory);

        s_loadingDefinitionFromDatabase(_logger);
        Task<MyDefinition> dbTask = Task.Run(async () =>
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ILoadDbDefinition<MyDefinition> loader = scope.ServiceProvider.GetRequiredService<ILoadDbDefinition<MyDefinition>>();
            return await loader.LoadDefinitionAsync(cancellationToken);
        }, cancellationToken);

        s_loadingDefinitionFromFileSystem(_logger);
        Task<MyDefinition> fileTask = Task.Run(() =>
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ILoadFsDefinition<MyDefinition> loader = scope.ServiceProvider.GetRequiredService<ILoadFsDefinition<MyDefinition>>();
            return loader.LoadDefinition(relaxed: false);
        }, cancellationToken);

        try
        {
            await Task.WhenAll(dbTask, fileTask);
        }
        catch (Exception ex)
        {
            if (dbTask.IsFaulted)
            {
                // throw dbTask.Exception!.InnerException!;
                throw;
            }
            if (fileTask.IsFaulted)
            {
                // throw fileTask.Exception!.InnerException!;
                throw;
            }
            s_unexpectedError(_logger, ex);
            throw;
        }

        MyDefinition startDef = dbTask.Result;
        MyDefinition endDef = fileTask.Result;

        List<Refactor> allUnappliedRefactors = await _unappliedRefactorLoader.GetAllUnappliedRefactorsAsync(_config.Schemas.Keys);
        List<DeployScript> deployScripts = await _unappliedDeployScriptLoader.GetAllUnappliedDeployScriptsAsync(_config.Schemas.Keys);

        s_comparingDefinitions(_logger);
        var differ = new MyDiffer(_config, startDef, endDef, allUnappliedRefactors, deployScripts, _logger);
        List<DefinitionAlterStatement> changes = differ.ComputeChanges();

        s_completedComputingChanges(_logger);

        StreamWriter? writer = null;
        try
        {
            if (outputFilePath != null)
            {
                writer = _fileWriter.GetFileStreamWriter(outputFilePath, fileExistsAction);
                if (writer == null)
                {
                    s_logOutputFileExists(_logger, outputFilePath);
                    return;
                }
                s_logWritingChanges(_logger, outputFilePath);
            }

            var statementWriter = new DifferStatementWriter(_config.DifferFormatting, writer);
            foreach (DefinitionAlterStatement change in changes)
            {
                statementWriter.WriteDefinitionAlter(change);
            }
        }
        finally
        {
            if (writer != null)
            {
                await writer.DisposeAsync();
            }
        }

    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Computing changes for schema definitions in '{RootDirectory}'...")]
    private static partial void s_computingChanges(ILogger logger, string rootDirectory);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Loading definition from database...")]
    private static partial void s_loadingDefinitionFromDatabase(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Loading definition from file system...")]
    private static partial void s_loadingDefinitionFromFileSystem(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Unexpected error loading definitions.")]
    private static partial void s_unexpectedError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Comparing definitions to compute changes...")]
    private static partial void s_comparingDefinitions(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Changes computed successfully.")]
    private static partial void s_completedComputingChanges(ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "File '{OutputFilePath}' exists. Skipping write.")]
    private static partial void s_logOutputFileExists(ILogger logger, string outputFilePath);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Writing change script to '{OutputFilePath}'...")]
    private static partial void s_logWritingChanges(ILogger logger, string outputFilePath);
}
