using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public interface IAddTablesToContext
{
    /// <summary>
    /// Adds any pseudo-tables referenced in this object to the provided pseudo-table set.
    /// This normally means adding to the pseudo-table set's "local" collection, indicating
    /// that the tables are available in the current context's SELECT item list.
    /// </summary>
    /// <param name="pseudoTableSet"></param>
    /// <param name="sourceRef"></param>
    public void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null);
}
