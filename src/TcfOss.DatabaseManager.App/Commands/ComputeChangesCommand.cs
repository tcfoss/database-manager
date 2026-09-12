using System.CommandLine;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.App.Commands;

public class ComputeChangesCommand(Action<ILogger>? loggerCallback = null, Action<SourceManager>? sourceManagerCallback = null) : CommandSpec(loggerCallback, sourceManagerCallback)
{
    protected override string CommandName => "compute-changes";
    protected override string Description => "Compute changes based on the current database state and write them to an SQL script file.";

    protected override bool NonGenericDialectRequired => true;
    protected override bool ConfigurationRequired => true;
    protected override bool ConnectionRequired => true;

    private static readonly Argument<string?> s_outputPathArgument = new("output_path")
    {
        Description = "Output file path for the computed changes.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    private static readonly Option<FileExistsAction> s_existsActionOption = new("--file-exists-action", "-f")
    {
        Description = "Action to take if the output file already exists. Options: Skip, Rename, Overwrite, Error.",
        Required = false,
        DefaultValueFactory = _ => FileExistsAction.Rename,
    };

    protected override IEnumerable<Argument> Arguments
    {
        get
        {
            yield return s_outputPathArgument;
        }
    }

    protected override IEnumerable<Option> Options
    {
        get
        {
            yield return s_existsActionOption;
        }
    }

    protected override async Task<int> ParseAndExecuteAsync(ParseResult parseResult, ExecutionContext context)
    {
        string outputPath = (parseResult.GetValue(s_outputPathArgument) ?? "changes.sql")
            .GetAbsolutePath(context.WorkingDirectory);
        FileExistsAction fileExistsAction = parseResult.GetValue(s_existsActionOption);

        await context.CommandRunner.ComputeChangesAsync(outputPath, fileExistsAction);
        return 0;
    }
}
