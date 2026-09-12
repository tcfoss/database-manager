using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Errors;

public abstract class SourcedException(string message, SourceRef? sourceRef = null) : Exception(message)
{
    public SourceRef? ObjectSourceRef { get; } = sourceRef;
}
