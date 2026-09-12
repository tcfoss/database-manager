using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public static class DefinitionHelpers
{
    public static MyDefinition SortDefinition(MyDefinition definition)
    {
        var newDef = new MyDefinition();
        foreach ((ObjectHandle tableHandle, MyTable table) in definition.Tables.OrderBy(t => t.Key))
        {
            var newKeys = new DatabaseComponentDict<MyKey>();
            foreach ((Handle keyId, MyKey key) in table.Keys.OrderBy(k => k.Key.Name))
            {
                newKeys.Add(keyId, key);
            }

            var newUniqueKeys = new DatabaseComponentDict<MyUniqueKey>();
            foreach ((Handle uniqueKeyId, MyUniqueKey uniqueKey) in table.UniqueKeys.OrderBy(k => k.Key.Name))
            {
                newUniqueKeys.Add(uniqueKeyId, uniqueKey);
            }

            var newForeignKeys = new DatabaseComponentDict<MyForeignKey>();
            foreach ((Handle foreignKeyId, MyForeignKey foreignKey) in table.ForeignKeys.OrderBy(k => k.Key.Name))
            {
                newForeignKeys.Add(foreignKeyId, foreignKey);
            }

            var newChecks = new DatabaseComponentDict<MyCheck>();
            foreach ((Handle checkId, MyCheck check) in table.Checks.OrderBy(k => k.Key.Name))
            {
                newChecks.Add(checkId, check);
            }

            newDef.Tables.Add(tableHandle, table with
            {
                Keys = newKeys,
                UniqueKeys = newUniqueKeys,
                ForeignKeys = newForeignKeys,
                Checks = newChecks
            });
        }
        foreach ((ObjectHandle viewId, MyView view) in definition.Views.OrderBy(v => v.Key))
        {
            newDef.Views.Add(viewId, view);
        }
        foreach ((ObjectHandle procedureId, MyStoredProcedure procedure) in definition.Procedures.OrderBy(sp => sp.Key))
        {
            newDef.Procedures.Add(procedureId, procedure);
        }
        foreach ((ObjectHandle functionId, MyStoredFunction function) in definition.Functions.OrderBy(f => f.Key))
        {
            newDef.Functions.Add(functionId, function);
        }
        foreach ((ObjectHandle triggerId, MyTrigger trigger) in definition.Triggers.OrderBy(t => t.Key))
        {
            newDef.Triggers.Add(triggerId, trigger);
        }
        foreach ((ObjectHandle eventId, MyEvent evt) in definition.Events.OrderBy(e => e.Key))
        {
            newDef.Events.Add(eventId, evt);
        }

        return newDef;
    }
}
