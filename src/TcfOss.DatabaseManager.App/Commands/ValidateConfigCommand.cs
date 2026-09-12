using System.CommandLine;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;

namespace TcfOss.DatabaseManager.App.Commands;

public class ValidateConfigCommand(Action<ILogger>? loggerCallback = null, Action<SourceManager>? sourceManagerCallback = null) : CommandSpec(loggerCallback, sourceManagerCallback)
{
    protected override string CommandName => "validate-config";
    protected override string Description => "Validate the configuration and optionally print it to the console.";

    protected override bool NonGenericDialectRequired => false;
    protected override bool ConfigurationRequired => true;
    protected override bool ConnectionRequired => false;

    private static readonly Option<bool> s_printConfigOption = new("--print-config", "-p")
    {
        Description = "Print the loaded configuration to the console. WARNING: The printed configuration is not necessarily a valid configuration file and may omit certain properties or include properties in a different format than expected in a config file. This flag is intended for debugging purposes only.",
        Required = false,
        DefaultValueFactory = _ => false
    };

    protected override IEnumerable<Argument> Arguments => [];

    protected override IEnumerable<Option> Options
    {
        get
        {
            yield return s_printConfigOption;
        }
    }

    protected override bool GetRelaxed(ParseResult parseResult) => true;

    protected override Task<int> ParseAndExecuteAsync(ParseResult parseResult, ExecutionContext context)
    {
        bool printConfig = parseResult.GetValue(s_printConfigOption);
        context.CommandRunner.ValidateConfiguration(printConfig, context.Out);
        return Task.FromResult(0);
    }
}
