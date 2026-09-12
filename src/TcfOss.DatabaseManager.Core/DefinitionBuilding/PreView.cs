using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public abstract record PreView(ObjectIdentifier Name, Select Body, PseudoTable Selectables)
{
    public SourceRef? SourceRef { get; init; }
    public string? RawBodyText { get; init; }

    public abstract View ToNormalized(SchemaIdentifier schemaId, IEnumerable<PseudoTable> pseudoTables, INormalizeSql normalizer);
}
