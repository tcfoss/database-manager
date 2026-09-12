using System.CommandLine;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.App.Commands;

public abstract class CommandSpec(Action<ILogger>? loggerCallback = null, Action<SourceManager>? sourceManagerCallback = null)
{
    public TextWriter Out { get; set; } = Console.Out;

    protected abstract string CommandName { get; }
    protected abstract string Description { get; }

    protected abstract bool NonGenericDialectRequired { get; }
    protected abstract bool ConfigurationRequired { get; }
    protected abstract bool ConnectionRequired { get; }

    protected abstract IEnumerable<Argument> Arguments { get; }
    protected abstract IEnumerable<Option> Options { get; }

    public Command Build()
    {
        Command command = new(CommandName, Description);
        foreach (Argument argument in Arguments)
        {
            command.Add(argument);
        }
        foreach (Option option in Options)
        {
            command.Add(option);
        }
        command.SetAction(RunAsync);
        return command;
    }

    protected abstract Task<int> ParseAndExecuteAsync(ParseResult parseResult, ExecutionContext context);

    private async Task<int> RunAsync(ParseResult parseResult)
    {
        ExecutionConfig execConfig = BootstrapHelpers.LoadExecutionConfig(parseResult);
        ValidateExecutionConfig(execConfig);

        ExecutionContext context = BootstrapHelpers.LoadExecutionContext(
            execConfig,
            relaxed: GetRelaxed(parseResult),
            outputWriter: Out,
            loggerCallback: loggerCallback,
            sourceManagerCallback: sourceManagerCallback
        );
        ValidateExecutionContext(context);

        return await ParseAndExecuteAsync(parseResult, context);
    }

    /// <summary>
    /// Returns whether the command should run in relaxed mode (i.e. with relaxed definition
    /// loading). Defaults to <see langword="false"/>. Override to return <see langword="true"/>
    /// for commands that don't require a strict definition, or to inspect
    /// <paramref name="parseResult"/> for a user-supplied flag.
    /// </summary>
    protected virtual bool GetRelaxed(ParseResult parseResult) => false;

    private void ValidateExecutionConfig(ExecutionConfig config)
    {
        if (config.Dialect == SqlDialect.Generic && NonGenericDialectRequired)
        {
            throw new CommandException.SpecificDialectRequired(CommandName);
        }
        if ((config.ConfigPath == null || !config.ConfigPath.Exists) && ConfigurationRequired)
        {
            throw new CommandException.ConfigurationRequired(CommandName);
        }
    }

    private void ValidateExecutionContext(ExecutionContext context)
    {
        if (!context.Config.DatabaseAvailable && ConnectionRequired)
        {
            throw new CommandException.ConnectionFailed(CommandName);
        }
    }
}
