namespace TcfOss.DatabaseManager.Core.Configuration;

public record DifferFormattingSettings
{
    public bool ObjectNamePrefixWithSchema { get; init; }
    public bool OmitModifiersIfDefault { get; init; } = true;

    public bool ProcedurePreferRawText { get; init; } = true;
    public bool FunctionPreferRawText { get; init; } = true;
    public bool TriggerPreferRawText { get; init; } = true;
    public bool ViewPreferRawText { get; init; } = true;
    public bool EventPreferRawText { get; init; } = true;
    public bool ProcedurePreferRawTextInScript { get; init; } = true;
    public bool FunctionPreferRawTextInScript { get; init; } = true;
    public bool TriggerPreferRawTextInScript { get; init; } = true;
    public bool ViewPreferRawTextInScript { get; init; } = true;
    public bool EventPreferRawTextInScript { get; init; } = true;
    public bool ViewPreferNormalizedBody { get; init; }

    public bool TerminateStatements { get; init; } = true;
    public bool UseDelimiterAroundPrograms { get; init; } = true;
    public bool UseDelimiterAroundViews { get; init; }

    public bool EmitCommentsWithWeights { get; init; }
}
