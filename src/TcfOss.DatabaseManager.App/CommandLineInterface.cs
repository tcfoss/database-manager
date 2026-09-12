using System.CommandLine;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.App.Commands;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.App;

public partial class CommandLineInterface
{
    public TextWriter Out
    {
        get;
        set
        {
            field = value;
            foreach (CommandSpec command in _commands)
            {
                command.Out = value;
            }
        }
    } = Console.Out;

    public TextWriter Error { get; set; } = Console.Error;

    private readonly RootCommand _rootCommand;

    private ILogger _logger;
    private SourceManager? _sourceManager;

    private readonly List<CommandSpec> _commands;

    public CommandLineInterface()
    {
        _logger = LoggingRegistration.GetBootstrapLogger();

        void SetLogger(ILogger logger) => _logger = logger;
        void SetSourceManager(SourceManager sourceManager) => _sourceManager = sourceManager;

        _commands =
        [
            new ParseFilesCommand(SetLogger, SetSourceManager),
            new ParseDefinitionCommand(SetLogger, SetSourceManager),
            new FormatSqlCommand(SetLogger, SetSourceManager),
            new DownloadSchemaCommand(SetLogger, SetSourceManager),
            new ComputeChangesCommand(SetLogger, SetSourceManager),
            new ValidateConfigCommand(SetLogger, SetSourceManager),
        ];

        _rootCommand = new RootCommand()
        {
            Description = $"Database Manager: A tool managing SQL databases and parsing SQL text.\n    Version: {BuildInfo.Version}\n    Build date: {BuildInfo.Date}",
            HelpName = "dbman"
        };
        BootstrapHelpers.AddGlobalOptions(_rootCommand);

        foreach (CommandSpec command in _commands)
        {
            _rootCommand.Add(command.Build());
        }
    }

    public int Run(string[] args)
    {
        ParseResult result = _rootCommand.Parse(args);

        var invocationConfiguration = new InvocationConfiguration
        {
            Output = Out,
            Error = Error,
            EnableDefaultExceptionHandler = false
        };

        try
        {
            return result.Invoke(invocationConfiguration);
        }
        catch (ParseException.WithText ex)
        {
            Error.WriteLine(ex.Message);
            s_criticalAppError(_logger, "Parse exception.", ex);
            return 1;
        }
        catch (LexException.WithText ex)
        {
            Error.WriteLine(ex.Message);
            s_criticalAppError(_logger, "Lexical exception.", ex);
            return 1;
        }
        catch (CommandException ex)
        {
            Error.WriteLine(ex.Message);
            return 1;
        }
        catch (ConfigParseException ex)
        {
            Error.WriteLine(ex.Message);
            return 1;
        }
        catch (ConfigurationException ex)
        {
            Error.WriteLine(ex.Message);
            return 1;
        }
        catch (SourcedException ex)
        {
            Error.WriteLine(ex.Message);
            if (ex.ObjectSourceRef.HasValue)
            {
                string? filename = _sourceManager?.GetFilename(ex.ObjectSourceRef.Value);
                if (filename != null)
                {
                    Error.WriteLine($"  Source: {filename}");
                }
            }
            s_criticalAppError(_logger, "Definition exception.", ex);
            return 1;
        }
        catch (FileNotFoundException ex)
        {
            Error.WriteLine($"File not found: {ex.Message}");
            s_criticalAppError(_logger, "File not found exception.", ex);
            return 1;
        }
        catch (DirectoryNotFoundException ex)
        {
            Error.WriteLine($"Directory not found: {ex.Message}");
            s_criticalAppError(_logger, "Directory not found exception.", ex);
            return 1;
        }
        catch (Exception ex)
        {
#if DEBUG
            string outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Errors");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"error_{Guid.NewGuid()}.json"), Serialization.ToJson(ex));
#endif
            Error.WriteLine("An unexpected error occurred. Check the logs for details.");
            s_criticalAppError(_logger, "Unknown exception.", ex);
            return 1;
        }
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Critical, Message = "{Message}")]
    private static partial void s_criticalAppError(ILogger logger, string message, Exception? ex);
}
