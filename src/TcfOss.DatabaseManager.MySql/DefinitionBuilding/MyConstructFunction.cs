using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyConstructFunction(MyConfig config, MyAttributeNormalizer normalizer, SourceManager sourceManager)
{
    private readonly MyConfig _config = config;
    private readonly MyAttributeNormalizer _normalizer = normalizer;
    private readonly SourceManager _sourceManager = sourceManager;

    public MyStoredFunction ConstructFunction(CreateFunction createFunctionStatement, ObjectIdentifier name, ExtendedQuoteStyle? accountQuoteStyle, SourceRef? sourceRef)
    {
        Definer definer = createFunctionStatement.Definer ?? new Definer(new Account.CurrentUser());
        definer = new Definer(_normalizer.NormalizeAccount(definer.Account, accountQuoteStyle));
        IEnumerable<RoutineParameter> parameters = createFunctionStatement.Parameters.Select(p => _normalizer.NormalizeRoutineParameter(p, true));
        return new MyStoredFunction(name, [.. parameters], createFunctionStatement.ReturnType, createFunctionStatement.Body)
        {
            Definer = definer,
            Aggregate = createFunctionStatement.Aggregate,
            SecurityContext = createFunctionStatement.MyCharacteristic?.SecurityContext ?? _config.AttributeDefaults.FunctionSecurityContext,
            Comment = createFunctionStatement.MyCharacteristic?.Comment,
            Deterministic = createFunctionStatement.MyCharacteristic?.Deterministic ?? false,
            SqlDataRelation = createFunctionStatement.MyCharacteristic?.Relation ?? _config.AttributeDefaults.FunctionDataRelation,
            RawBodyText = _sourceManager.GetText(sourceRef, createFunctionStatement.Body.Meta),
        };
    }
}
