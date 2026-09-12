using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyRelaxedDefinitionBuilder(MyConfig config, SourceManager sourceManager, IFunctionNameProvider functionNameProvider, ILogger<MyDefinitionBuilder> logger)
    : MyDefinitionBuilder(config, sourceManager, functionNameProvider, logger)
{
    private readonly ValidationSettings _validationSettings = config.ValidationSettings;

    protected override List<MyPreTrigger>? ValidatePreTrigger(CreateTrigger createTriggerStatement, MyPreTrigger preTrigger, SourceRef? sourceRef)
    {
        var triggerKey = new PreTriggerKey(preTrigger.OnTable, preTrigger.TriggerTime, preTrigger.TriggerEvent);
        return PreTriggers.GetValueOrDefault(triggerKey);
    }

    protected override DatabaseObjectDict<MyTrigger> GetTriggers()
    {
        var triggers = new DatabaseObjectDict<MyTrigger>(Config.NameHandling);
        foreach ((PreTriggerKey _, List<MyPreTrigger> preTriggers) in PreTriggers)
        {
            var orderedPreTriggers = new List<MyPreTrigger>();

            var remainingPreTriggers = preTriggers.ToList();

            foreach (MyPreTrigger preTrigger in preTriggers.Where(x => x.AfterTrigger == null))
            {
                orderedPreTriggers.Add(preTrigger);
                remainingPreTriggers.Remove(preTrigger);
            }

            while (remainingPreTriggers.Count > 0)
            {
                MyPreTrigger nextTrigger = remainingPreTriggers[0];

                MyPreTrigger lastConsistentTrigger = orderedPreTriggers.Last(x => x.Name == nextTrigger.AfterTrigger);
                int lastConsistentTriggerIndex = orderedPreTriggers.IndexOf(lastConsistentTrigger);

                orderedPreTriggers.Insert(lastConsistentTriggerIndex + 1, nextTrigger);
                remainingPreTriggers.Remove(nextTrigger);
            }

            for (int i = 0; i < orderedPreTriggers.Count; i++)
            {
                MyPreTrigger curr = orderedPreTriggers[i];
                triggers[curr.Name] = new MyTrigger(curr.Name, curr.OnTable, curr.TriggerTime, curr.TriggerEvent, curr.Body)
                {
                    Definer = curr.Definer,
                    Order = (uint)(i + 1),
                    RawBodyText = curr.RawBodyText
                };
            }
        }

        return triggers;
    }

    protected override void ValidateDefiner(Definer? definer, string objectType, ObjectIdentifier objectId, SourceRef? sourceRef)
    {
        if (_validationSettings.AllowMissingDefiner)
        {
            return;
        }
        if (definer == null)
        {
            throw new DefinitionException.MissingDefiner(objectType, objectId, sourceRef);
        }
        if (_validationSettings.AllowImplicitDefiner)
        {
            return;
        }
        if (definer.Account is Account.CurrentRole or Account.CurrentUser)
        {
            throw new DefinitionException.ImplicitDefiner(objectType, objectId, sourceRef);
        }
    }

    protected override void ValidateSecurityContext(SecurityContext? securityContext, string objectType, ObjectIdentifier objectId, SourceRef? sourceRef)
    {
        if (_validationSettings.AllowMissingSecurityContext)
        {
            return;
        }
        if (securityContext == null)
        {
            throw new DefinitionException.MissingSecurityContext(objectType, objectId, sourceRef);
        }
    }

    protected override List<SourcedException> ValidateForeignKeyBackingIndexExists(MyForeignKey key, MyTable table, SourceRef? sourceRef)
    {
        return [];
    }

    protected override List<SourcedException> ValidateForeignKeyReferencedColumnsBackingIndexExists(MyForeignKey key, MyTable table, MyTable referencedTable, SourceRef? sourceRef)
    {
        return [];
    }

    protected override void ValidateCreateTable(CreateTable createTableStatement, ObjectIdentifier tableId, SourceRef? sourceRef)
    {
        if (!_validationSettings.AllowCreateTableAsSelect && createTableStatement.AsSelect != null)
        {
            throw new DefinitionException.CreateTableAsSelect(tableId, sourceRef);
        }

        if (createTableStatement.Columns.SafeAny())
        {
            return;
        }

        if (_validationSettings.AllowCreateTableNoColumns)
        {
            // Do nothing.
        }
        else
        {
            throw new DefinitionException.MissingTableColumns(tableId, sourceRef);
        }
    }
}
