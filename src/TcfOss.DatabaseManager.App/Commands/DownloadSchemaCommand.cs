using System.CommandLine;

namespace TcfOss.DatabaseManager.App.Commands;

public class DownloadSchemaCommand : CommandSpec
{
    protected override string CommandName => "download-schema";
    protected override string Description => "Download the database schema using the specified configuration.";

    protected override bool NonGenericDialectRequired => true;
    protected override bool ConfigurationRequired => true;
    protected override bool ConnectionRequired => true;

    protected override IEnumerable<Argument> Arguments => [];
    protected override IEnumerable<Option> Options => [];

    public override async Task<int> ParseAndExecuteAsync(ParseResult parseResult, ExecutionContext context)
    {
        await context.CommandRunner.DownloadSchemaAsync();
        return 0;
    }
}
