using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyConstructPreTrigger(MyAttributeNormalizer normalizer, SourceManager sourceManager)
{
    private readonly MyAttributeNormalizer _normalizer = normalizer;
    private readonly SourceManager _sourceManager = sourceManager;

    public MyPreTrigger ConstructTrigger(CreateTrigger createTriggerStatement, ObjectIdentifier name, QuoteStyle quoteStyle, ExtendedQuoteStyle? accountQuoteStyle, SourceRef? sourceRef)
    {
        var onTable = ObjectIdentifier.FromObjectName(createTriggerStatement.TableName, name.Schema, quoteStyle);
        ObjectIdentifier? afterTrigger = null;
        ObjectIdentifier? beforeTrigger = null;

        if (createTriggerStatement.Order != null)
        {
            var relTrigger = ObjectIdentifier.FromObjectName(createTriggerStatement.Order.OtherTrigger, name.Schema, quoteStyle);
            if (createTriggerStatement.Order.Position == TriggerOrderPosition.Precedes)
            {
                beforeTrigger = relTrigger;
            }
            else
            {
                afterTrigger = relTrigger;
            }
        }
        string? rawBodyText = _sourceManager.GetText(sourceRef, createTriggerStatement.Body.Meta);

        Definer definer = createTriggerStatement.Definer ?? new Definer(new Account.CurrentUser());
        definer = new Definer(_normalizer.NormalizeAccount(definer.Account, accountQuoteStyle));

        if (createTriggerStatement.Events.Count != 1)
        {
            throw new SqlSyntaxException.TriggerSupportsOnlyOneEvent("MySQL/MariaDB", name.ToString(), createTriggerStatement.Events.Count, sourceRef);
        }

        return new MyPreTrigger(name, onTable, createTriggerStatement.TriggerTime, createTriggerStatement.Events[0], createTriggerStatement.Body)
        {
            Definer = definer,
            AfterTrigger = afterTrigger,
            BeforeTrigger = beforeTrigger,
            RawBodyText = rawBodyText
        };
    }
}
