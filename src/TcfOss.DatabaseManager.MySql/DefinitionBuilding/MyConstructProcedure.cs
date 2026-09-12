using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyConstructProcedure(MyConfig config, MyAttributeNormalizer normalizer, SourceManager sourceManager)
{
    private readonly MyConfig _config = config;
    private readonly MyAttributeNormalizer _normalizer = normalizer;
    private readonly SourceManager _sourceManager = sourceManager;

    public MyStoredProcedure ConstructProcedure(CreateProcedure createProcedureStatement, ObjectIdentifier name, ExtendedQuoteStyle? accountQuoteStyle, SourceRef? sourceRef)
    {
        IEnumerable<RoutineParameter> parameters = createProcedureStatement.Parameters.Select(p => _normalizer.NormalizeRoutineParameter(p, false));
        Definer definer = createProcedureStatement.Definer ?? new Definer(new Account.CurrentUser());
        definer = new Definer(_normalizer.NormalizeAccount(definer.Account, accountQuoteStyle));

        return new MyStoredProcedure(name, [.. parameters], createProcedureStatement.Body)
        {
            Definer = definer,
            SecurityContext = createProcedureStatement.MyCharacteristic?.SecurityContext ?? _config.AttributeDefaults.ProcedureSecurityContext,
            Comment = createProcedureStatement.MyCharacteristic?.Comment,
            Deterministic = createProcedureStatement.MyCharacteristic?.Deterministic ?? false,
            SqlDataRelation = createProcedureStatement.MyCharacteristic?.Relation ?? _config.AttributeDefaults.ProcedureDataRelation,
            RawBodyText = _sourceManager.GetText(sourceRef, createProcedureStatement.Body.Meta),
        };
    }
}
