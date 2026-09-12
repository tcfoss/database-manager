using System.CommandLine;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.App.Commands;

public class ParseFilesCommand(Action<ILogger>? loggerCallback = null, Action<SourceManager>? sourceManagerCallback = null) : CommandSpec(loggerCallback, sourceManagerCallback)
{

    protected override string CommandName => "parse-files";
    protected override string Description => "Parse SQL files and emit the results to JSON files.";

    protected override bool NonGenericDialectRequired => false;
    protected override bool ConfigurationRequired => false;
    protected override bool ConnectionRequired => false;

    private static readonly Argument<string[]> s_filesArgument = new("files")
    {
        Description = "List of SQL files to parse.",
        Arity = ArgumentArity.OneOrMore
    };

    private static readonly Option<string> s_outputPatternOption = new("--output-pattern", "-o")
    {
        Description = "Output file pattern for parsed results. Use '{fileName}' to include the original file name.",
        Required = false,
        DefaultValueFactory = _ => "{fileName}.parsed.json"
    };

    private static readonly Option<bool> s_includeMetaOption = new("--include-meta", "-m")
    {
        Description = "Include non-SQL information in the parsed output.",
        Required = false,
    };

    private static readonly Option<FileExistsAction> s_existsActionOption = new("--file-exists-action", "-f")
    {
        Description = "Action to take if the output file already exists. Options: Skip, Rename, Overwrite, Error.",
        Required = false,
        DefaultValueFactory = _ => FileExistsAction.Skip
    };

    protected override IEnumerable<Argument> Arguments
    {
        get
        {
            yield return s_filesArgument;
        }
    }

    protected override IEnumerable<Option> Options
    {
        get
        {
            yield return s_outputPatternOption;
            yield return s_includeMetaOption;
            yield return s_existsActionOption;
        }
    }

    protected override bool GetRelaxed(ParseResult parseResult) => true;

    protected override Task<int> ParseAndExecuteAsync(ParseResult parseResult, ExecutionContext context)
    {
        string[] rawFiles = parseResult.GetValue(s_filesArgument)!;
        string outputPattern = parseResult.GetValue(s_outputPatternOption)!.GetAbsolutePath(context.WorkingDirectory);
        bool includeMeta = parseResult.GetValue(s_includeMetaOption);
        FileExistsAction fileExistsAction = parseResult.GetValue(s_existsActionOption);

        List<string> files = [];

        foreach (string raw in rawFiles)
        {
            if (raw.Contains('*') || raw.Contains('?') || raw.Contains('['))
            {
                var matcher = new Matcher();
                matcher.AddInclude(raw);
                IEnumerable<string> matches = matcher.GetResultsInFullPath(context.WorkingDirectory);
                files.AddRange(matches);
            }
            else
            {
                string absolutePath = raw.GetAbsolutePath(context.WorkingDirectory);
                if (!File.Exists(absolutePath))
                {
                    throw new FileNotFoundException(absolutePath);
                }
                files.Add(absolutePath);
            }
        }

        context.CommandRunner.ParseFiles([.. files], outputPattern, includeMeta, fileExistsAction);
        return Task.FromResult(0);
    }
}
