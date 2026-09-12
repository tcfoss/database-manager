using System.CommandLine;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.App.Commands;

public class ParseDefinitionCommand(Action<ILogger>? loggerCallback = null, Action<SourceManager>? sourceManagerCallback = null) : CommandSpec(loggerCallback, sourceManagerCallback)
{

    protected override string CommandName => "parse-definition";
    protected override string Description => "Parse the database definition from the files indicated in the configuration and write it to a JSON file.";

    protected override bool NonGenericDialectRequired => true;
    protected override bool ConfigurationRequired => true;
    protected override bool ConnectionRequired => false;

    private static readonly Argument<string> s_outputPathArgument = new("output_path")
    {
        Description = "Output file path for the parsed definition.",
        Arity = ArgumentArity.ExactlyOne
    };

    private static readonly Option<bool> s_relaxedOption = new("--relaxed", "-r")
    {
        Description = "Enable relaxed parsing mode.",
        Required = false,
    };

    private static readonly Option<bool> s_includeMetaOption = new("--include-meta", "-m")
    {
        Description = "Include non-SQL information in the parsed output.",
        Required = false,
    };

    private static readonly Option<bool> s_includeRawTextOption = new("--include-raw-text", "-t")
    {
        Description = "Include raw SQL text in the parsed output.",
        Required = false,
    };

    private static readonly Option<bool> s_includeSourceRefOption = new("--include-source-ref", "-s")
    {
        Description = "Include source reference information in the parsed output.",
        Required = false,
    };

    private static readonly Option<FileExistsAction> s_existsActionOption = new("--file-exists-action", "-f")
    {
        Description = "Action to take if the output file already exists. Options: Skip, Rename, Overwrite, Error.",
        Required = false,
        DefaultValueFactory = _ => FileExistsAction.Error
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
            yield return s_relaxedOption;
            yield return s_includeMetaOption;
            yield return s_includeRawTextOption;
            yield return s_includeSourceRefOption;
            yield return s_existsActionOption;
        }
    }

    protected override bool GetRelaxed(ParseResult parseResult) => parseResult.GetValue(s_relaxedOption);

    protected override Task<int> ParseAndExecuteAsync(ParseResult parseResult, ExecutionContext context)
    {
        bool relaxed = GetRelaxed(parseResult);
        string? outputPath = parseResult.GetValue(s_outputPathArgument);
        if (string.IsNullOrEmpty(outputPath))
        {
            throw new CommandException.RequiredArgumentMissing("output_path");
        }

        outputPath = outputPath.GetAbsolutePath(context.WorkingDirectory);

        bool includeMeta = parseResult.GetValue(s_includeMetaOption);
        bool includeRawText = parseResult.GetValue(s_includeRawTextOption);
        bool includeSourceRef = parseResult.GetValue(s_includeSourceRefOption);
        FileExistsAction fileExistsAction = parseResult.GetValue(s_existsActionOption);

        context.CommandRunner.ParseDefinition(outputPath, relaxed: relaxed, includeMeta: includeMeta, includeRawText: includeRawText, includeSourceRef: includeSourceRef, fileExistsAction: fileExistsAction);

        return Task.FromResult(0);
    }
}
