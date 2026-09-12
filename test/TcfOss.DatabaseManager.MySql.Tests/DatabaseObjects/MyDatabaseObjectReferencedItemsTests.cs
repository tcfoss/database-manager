using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseObjects;

public class MyDatabaseObjectReferencedItemsTests
{
    private static readonly CatalogIdentifier s_catalog = new("def");
    private static readonly SchemaIdentifier s_schema = new("myschema", s_catalog);

    private static ReferencedItemsManager CreateManager()
    {
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create(new ObjectIdentifier("mytable", s_schema), NameHandling.None)] = ObjectType.Table,
        });
        var pseudoTables = new PseudoTableSet(s_schema, []);
        return new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.Table,
            Definition = provider,
            ActiveSchema = s_schema,
            PseudoTables = pseudoTables,
            FunctionNameProvider = new FunctionNameProvider(),
        };
    }

    [Fact]
    public void MyEvent_GetReferencedItems_DelegatesToBody()
    {
        var body = new Select(
            new SelectBody.SimpleSelectQuery(
                new SimpleSelect([new SimpleSelectItem.Wildcard()])
                {
                    From = [new TableWithJoins(new TableFactor.Table(new ObjectName(new Identifier("mytable"))))]
                }));

        var myEvent = new MyEvent(
            new ObjectIdentifier("myevent", s_schema),
            new EventSchedule.Every(new LiteralValue(new Value.Number("5", false)), DateTimeUnit.Day),
            body)
        {
            Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("user"), new ExtendedIdentifier("host"))),
            EventEnabledStatus = EventEnabledStatus.Enable,
            OnCompletionPreserve = false,
        };

        List<ItemRef> items = [.. myEvent.GetReferencedItems(CreateManager())];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", item.N());
    }
}
