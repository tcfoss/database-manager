using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public record PreTrigger(ObjectIdentifier Name, ObjectIdentifier OnTable, Statement Body)
{
    public string? RawBodyText { get; init; }
    public SourceRef? SourceRef { get; init; }
}
