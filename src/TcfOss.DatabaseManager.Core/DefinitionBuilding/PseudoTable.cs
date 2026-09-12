using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

/// <summary>
/// A pseudo-table is an object that can be SELECTed from. The primary examples
/// are tables and views.
///
/// If the Identifier is given, it should represent an actual database object.
/// If it is null, the pseudo-table is a derived table (for example, a CTE or
/// a subquery in a FROM clause).
/// </summary>
/// <param name="Name"></param>
/// <param name="Identifier"></param>
/// <param name="SelectableItems"></param>
public record PseudoTable(string Name, ObjectIdentifier? Identifier, List<string> SelectableItems, PseudoTableType Type)
{
    public string? Alias { get; init; }
    public SourceRef? SourceRef { get; init; }

    public record Unbound(string Name, ObjectIdentifier? Identifier, PseudoTableType Type)
        : PseudoTable(Name, Identifier, [], Type);
}
