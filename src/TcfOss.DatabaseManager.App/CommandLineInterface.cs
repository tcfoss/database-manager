using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.App.Commands;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.App;

public partial class CommandLineInterface
{
    public TextWriter Out { get; set; } = Console.Out;

    public TextWriter Error { get; set; } = Console.Error;

    private readonly RootCommand _rootCommand;

    private readonly ILogger _bootstrapLogger;

    public CommandLineInterface()
    {
        _bootstrapLogger = LoggingRegistration.GetBootstrapLogger();

        CommandSpec[] commands =
        [
            new ParseFilesCommand(),
            new ParseDefinitionCommand(),
            new FormatSqlCommand(),
            new DownloadSchemaCommand(),
            new ComputeChangesCommand(),
            new ValidateConfigCommand(),
        ];

        _rootCommand = new RootCommand()
        {
            Description = $"Database Manager: A tool managing SQL databases and parsing SQL text.\n    Version: {BuildInfo.Version}\n    Build date: {BuildInfo.Date}",
            HelpName = "dbman"
        };
        BootstrapHelpers.AddGlobalOptions(_rootCommand);

        foreach (CommandSpec command in commands)
        {
            _rootCommand.Add(command.Build(parseResult => RunCommandAsync(command, parseResult)));
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

        return result.Invoke(invocationConfiguration);
    }

    private async Task<int> RunCommandAsync(CommandSpec command, ParseResult parseResult)
    {
        ILogger logger = _bootstrapLogger;
        SourceManager? sourceManager = null;
        ExecutionContext? context = null;
        try
        {
            ExecutionConfig execConfig = BootstrapHelpers.LoadExecutionConfig(parseResult);
            command.ValidateExecutionConfig(execConfig);

            context = BootstrapHelpers.LoadExecutionContext(
                execConfig,
                relaxed: command.GetRelaxed(parseResult),
                outputWriter: Out);

            logger = context.Logger;
            sourceManager = context.SourceManager;

            command.ValidateExecutionContext(context);

            return await command.ParseAndExecuteAsync(parseResult, context);
        }
        catch (ParseException.WithText ex)
        {
            await Error.WriteLineAsync(ex.Message);
            s_criticalAppError(logger, "Parse exception.", ex);
            return ExitCodes.ParseError;
        }
        catch (LexException.WithText ex)
        {
            await Error.WriteLineAsync(ex.Message);
            s_criticalAppError(logger, "Lexical exception.", ex);
            return ExitCodes.ParseError;
        }
        catch (CommandException ex)
        {
            await Error.WriteLineAsync(ex.Message);
            return ExitCodes.OtherError;
        }
        catch (ConfigParseException ex)
        {
            await Error.WriteLineAsync(ex.Message);
            return ExitCodes.ConfigurationError;
        }
        catch (ConfigurationException ex)
        {
            await Error.WriteLineAsync(ex.Message);
            return ExitCodes.ConfigurationError;
        }
        catch (SourcedException ex)
        {
            await Error.WriteLineAsync(ex.Message);
            if (ex.ObjectSourceRef.HasValue)
            {
                string? filename = sourceManager!.GetFilename(ex.ObjectSourceRef.Value);
                if (filename != null)
                {
                    await Error.WriteLineAsync($"  Source: {filename}");
                }
            }
            s_criticalAppError(logger, "Definition exception.", ex);
            return ex switch
            {
                DefinitionException => ExitCodes.DefinitionError,
                SqlSyntaxException => ExitCodes.SqlSyntaxError,
                ValidationExceptionSet ves when ves.Exceptions.All(e => e is DefinitionException) => ExitCodes.DefinitionError,
                ValidationExceptionSet => ExitCodes.SqlSyntaxError,
                _ => ExitCodes.OtherError
            };
        }
        catch (FileNotFoundException ex)
        {
            await Error.WriteLineAsync($"File not found: {ex.Message}");
            s_criticalAppError(logger, "File not found exception.", ex);
            return ExitCodes.NotFoundError;
        }
        catch (DirectoryNotFoundException ex)
        {
            await Error.WriteLineAsync($"Directory not found: {ex.Message}");
            s_criticalAppError(logger, "Directory not found exception.", ex);
            return ExitCodes.NotFoundError;
        }
        catch (Exception ex)
        {
            return HandleUnexpectedError(ex);
        }
        finally
        {
            context?.Dispose();
        }

        [ExcludeFromCodeCoverage]
        int HandleUnexpectedError(Exception ex)
        {
#if DEBUG
            string outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Errors");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"error_{Guid.NewGuid()}.json"), Serialization.ToJson(ex));
#endif
            Error.WriteLine("An unexpected error occurred. Check the logs for details.");
            s_criticalAppError(logger, "Unknown exception.", ex);
            return ExitCodes.OtherError;
        }
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Critical, Message = "{Message}")]
    private static partial void s_criticalAppError(ILogger logger, string message, Exception? ex);
}
