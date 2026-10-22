using System.CommandLine;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.App.Commands;

public abstract class CommandSpec
{
    protected abstract string CommandName { get; }
    protected abstract string Description { get; }

    protected abstract bool NonGenericDialectRequired { get; }
    protected abstract bool ConfigurationRequired { get; }
    protected abstract bool ConnectionRequired { get; }

    protected abstract IEnumerable<Argument> Arguments { get; }
    protected abstract IEnumerable<Option> Options { get; }

    public Command Build(Func<ParseResult, Task<int>> executeCommand)
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
        command.SetAction(executeCommand);
        return command;
    }

    public abstract Task<int> ParseAndExecuteAsync(ParseResult parseResult, ExecutionContext context);

    /// <summary>
    /// Returns whether the command should run in relaxed mode (i.e. with relaxed definition
    /// loading). Defaults to <see langword="false"/>. Override to return <see langword="true"/>
    /// for commands that don't require a strict definition, or to inspect
    /// <paramref name="parseResult"/> for a user-supplied flag.
    /// </summary>
    public virtual bool GetRelaxed(ParseResult parseResult) => false;

    public void ValidateExecutionConfig(ExecutionConfig config)
    {
        if (config.Dialect == SqlDialect.Generic && NonGenericDialectRequired)
        {
            throw new CommandException.SpecificDialectRequired(CommandName);
        }
        if (ConfigurationRequired)
        {
            if (config.ConfigPath == null)
            {
                throw new CommandException.ConfigurationRequired(CommandName);
            }
            if (!config.ConfigPath.Exists)
            {
                throw new CommandException.ConfigurationDoesNotExist(CommandName, config.ConfigPath.FullName);
            }
        }
    }

    public void ValidateExecutionContext(ExecutionContext context)
    {
        if (!context.Config.DatabaseAvailable && ConnectionRequired)
        {
            throw new CommandException.ConnectionFailed(CommandName);
        }
    }
}
