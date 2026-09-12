using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyConstructPreView(MyConfig config, MyAttributeNormalizer normalizer, SourceManager sourceManager)
{
    private readonly MyConfig _config = config;
    private readonly MyAttributeNormalizer _normalizer = normalizer;
    private readonly SourceManager _sourceManager = sourceManager;

    public MyPreView ConstructPreView(CreateView createViewStatement, ObjectIdentifier name, ExtendedQuoteStyle? accountQuoteStyle, SourceRef? sourceRef)
    {
        Definer definer = createViewStatement.Definer ?? new Definer(new Account.CurrentUser());
        definer = new Definer(_normalizer.NormalizeAccount(definer.Account, accountQuoteStyle));

        string? rawBodyText = _sourceManager.GetText(sourceRef, createViewStatement.Body.Meta);

        return new MyPreView(name, createViewStatement.Body, createViewStatement.Body.ToPseudoTable(name.Name, name, sourceRef, PseudoTableType.View))
        {
            Definer = definer,
            SecurityContext = createViewStatement.SecurityContext ?? _config.AttributeDefaults.ViewSecurityContext,
            Algorithm = createViewStatement.ViewAlgorithm ?? ViewAlgorithm.Undefined,
            CheckOption = createViewStatement.ViewCheckOption,
            RawBodyText = rawBodyText,
            SourceRef = sourceRef,
        };
    }
}
