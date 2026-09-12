using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public record MyPreView(ObjectIdentifier Name, Select Body, PseudoTable Selectables) : PreView(Name, Body, Selectables)
{
    public required Definer Definer { get; init; }
    public required SecurityContext SecurityContext { get; init; }
    public required ViewAlgorithm Algorithm { get; init; }
    public ViewCheckOption? CheckOption { get; init; }

    public override MyView ToNormalized(SchemaIdentifier schemaId, IEnumerable<PseudoTable> pseudoTables, INormalizeSql normalizer)
    {
        var selectableSourceSet = new PseudoTableSet(schemaId, pseudoTables);
        return new MyView(Name, Body)
        {
            Definer = Definer,
            SecurityContext = SecurityContext,
            Algorithm = Algorithm,
            CheckOption = CheckOption,
            RawBodyText = RawBodyText,
            NormalizedBody = normalizer.NormalizeSelect(Body, schemaId, selectableSourceSet, Name, SourceRef)
        };
    }
}
