using System.CommandLine;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;

namespace TcfOss.DatabaseManager.App.Commands;

public class DownloadSchemaCommand(Action<ILogger>? loggerCallback = null, Action<SourceManager>? sourceManagerCallback = null) : CommandSpec(loggerCallback, sourceManagerCallback)
{
    protected override string CommandName => "download-schema";
    protected override string Description => "Download the database schema using the specified configuration.";

    protected override bool NonGenericDialectRequired => true;
    protected override bool ConfigurationRequired => true;
    protected override bool ConnectionRequired => true;

    protected override IEnumerable<Argument> Arguments => [];
    protected override IEnumerable<Option> Options => [];

    protected override async Task<int> ParseAndExecuteAsync(ParseResult parseResult, ExecutionContext context)
    {
        await context.CommandRunner.DownloadSchemaAsync();
        return 0;
    }
}
