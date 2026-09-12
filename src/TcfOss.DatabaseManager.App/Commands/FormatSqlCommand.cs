using System.CommandLine;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.App.Commands;

public class FormatSqlCommand(Action<ILogger>? loggerCallback = null, Action<SourceManager>? sourceManagerCallback = null) : CommandSpec(loggerCallback, sourceManagerCallback)
{

    protected override string CommandName => "format-sql";
    protected override string Description => "Format SQL files using the specified configuration.";

    protected override bool NonGenericDialectRequired => false;
    protected override bool ConfigurationRequired => false;
    protected override bool ConnectionRequired => false;

    private static readonly Argument<string[]> s_filesArgument = new("files")
    {
        Description = "List of SQL files to format.",
        Arity = ArgumentArity.OneOrMore
    };

    private static readonly Option<string?> s_outputPatternOption = new("--output-pattern", "-o")
    {
        Description = "Output file pattern for formatted SQL files. Use '{fileName}' to include the original file name.",
        Required = false,
    };

    private static readonly Option<bool> s_noBackupOption = new("--no-backup", "-n")
    {
        Description = "Do not create backup files when overwriting existing files.",
        Required = false,
    };

    private static readonly Option<FileExistsAction> s_existsActionOption = new("--file-exists-action", "-f")
    {
        Description = "Action to take if the output file already exists. Options: Skip, Rename, Overwrite, Error.",
        Required = false,
        DefaultValueFactory = _ => FileExistsAction.Overwrite
    };

    private static readonly Option<bool> s_strictOption = new("--strict", "-s")
    {
        Description = "Enable strict mode for loading the database definition.",
        Required = false,
    };

    private static readonly Option<string?> s_definitionFileOption = new("--definition-file")
    {
        Description = "Path to a pre-parsed definition JSON file to use for formatting. If not provided, the definition is loaded from the filesystem.",
        Required = false,
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
            yield return s_noBackupOption;
            yield return s_existsActionOption;
            yield return s_strictOption;
            yield return s_definitionFileOption;
        }
    }

    protected override bool GetRelaxed(ParseResult parseResult) => !parseResult.GetValue(s_strictOption);

    protected override Task<int> ParseAndExecuteAsync(ParseResult parseResult, ExecutionContext context)
    {
        string[] rawFiles = parseResult.GetValue(s_filesArgument)!;
        string? outputPattern = parseResult.GetValue(s_outputPatternOption);
        bool noBackup = parseResult.GetValue(s_noBackupOption);
        FileExistsAction fileExistsAction = parseResult.GetValue(s_existsActionOption);
        string? definitionFile = parseResult.GetValue(s_definitionFileOption)?.GetAbsolutePath(context.WorkingDirectory);
        bool relaxed = GetRelaxed(parseResult);

        List<string> files = [];

        foreach (string raw in rawFiles)
        {
            if (raw.Contains('*') || raw.Contains('?') || raw.Contains('['))
            {
                var matcher = new Matcher();
                matcher.AddInclude(raw);
                files.AddRange(matcher.GetResultsInFullPath(context.WorkingDirectory));
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

        context.CommandRunner.FormatSql([.. files], outputPattern, noBackup, relaxed, fileExistsAction, definitionFile);
        return Task.FromResult(0);
    }
}
