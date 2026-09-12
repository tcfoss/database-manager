using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public readonly struct LogDelegateGetFileWrapper(SourceManager sourceManager, SourceRef? sourceRef)
{
    public override string ToString()
    {
        return sourceManager.GetFilename(sourceRef) ?? "unknown";
    }
}
