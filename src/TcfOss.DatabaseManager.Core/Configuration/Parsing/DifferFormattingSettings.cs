namespace TcfOss.DatabaseManager.Core.Configuration.Parsing;

public class DifferFormattingSettings
{
    public bool? ObjectNamePrefixWithSchema { get; set; }
    public bool? OmitModifiersIfDefault { get; set; }

    public bool? PreferRawText { get; set; }
    public bool? ProcedurePreferRawText { get; set; }
    public bool? FunctionPreferRawText { get; set; }
    public bool? TriggerPreferRawText { get; set; }
    public bool? ViewPreferRawText { get; set; }
    public bool? EventPreferRawText { get; set; }
    public bool? PreferRawTextInScript { get; set; }
    public bool? ProcedurePreferRawTextInScript { get; set; }
    public bool? FunctionPreferRawTextInScript { get; set; }
    public bool? TriggerPreferRawTextInScript { get; set; }
    public bool? ViewPreferRawTextInScript { get; set; }
    public bool? EventPreferRawTextInScript { get; set; }
    public bool? ViewPreferNormalizedBody { get; set; }

    public bool? TerminateStatements { get; set; } = true;
    public bool? UseDelimiterAroundPrograms { get; set; }
    public bool? UseDelimiterAroundViews { get; set; }

    public bool? EmitCommentsWithWeights { get; set; }
}
