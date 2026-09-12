using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Tests.DatabaseObjects;

public class DatabaseObjectReferencedItemsTests
{
    private static readonly CatalogIdentifier s_catalog = new("def");
    private static readonly SchemaIdentifier s_schema = new("myschema", s_catalog);

    private static string N(ItemRef item) => string.Join(".", item.Identifiers.Select(i => i.Name));

    private static ReferencedItemsManager CreateManager()
    {
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create(new ObjectIdentifier("mytable", s_schema), NameHandling.None)] = ObjectType.Table,
            [ObjectHandle.Create(new ObjectIdentifier("anothertable", s_schema), NameHandling.None)] = ObjectType.Table,
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

    private static SimpleSelect MakeSimpleSelect(string tableName) =>
        new([new SimpleSelectItem.Wildcard()])
        {
            From = [new TableWithJoins(new TableFactor.Table(new ObjectName(new Identifier(tableName))))]
        };

    private static Select MakeSelect(string tableName) =>
        new(new SelectBody.SimpleSelectQuery(MakeSimpleSelect(tableName)));

    private record TestView(ObjectIdentifier Name, Select Body) : View(Name, Body)
    {
        public override CreateView ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null) =>
            throw new NotSupportedException();
    }

    [Fact]
    public void View_GetReferencedItems_DelegatesToBody()
    {
        var view = new TestView(new ObjectIdentifier("myview", s_schema), MakeSelect("mytable"));

        List<ItemRef> items = [.. view.GetReferencedItems(CreateManager())];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", N(item));
    }

    [Fact]
    public void StoredProcedure_GetReferencedItems_DelegatesToBody()
    {
        var procedure = new StoredProcedure(new ObjectIdentifier("myproc", s_schema), [], MakeSelect("mytable"));

        List<ItemRef> items = [.. procedure.GetReferencedItems(CreateManager())];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", N(item));
    }

    [Fact]
    public void StoredFunction_GetReferencedItems_DelegatesToBody()
    {
        var function = new StoredFunction(new ObjectIdentifier("myfunc", s_schema), [], new DataType.Int(), MakeSelect("mytable"));

        List<ItemRef> items = [.. function.GetReferencedItems(CreateManager())];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("mytable", N(item));
    }

    [Fact]
    public void Trigger_GetReferencedItems_EmitsOnTable()
    {
        var trigger = new Trigger(
            new ObjectIdentifier("mytrigger", s_schema),
            new ObjectIdentifier("mytable", s_schema),
            new Commit());

        List<ItemRef> items = [.. trigger.GetReferencedItems(CreateManager())];

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Table, item.Type);
        Assert.Equal("myschema.mytable", N(item));
    }

    [Fact]
    public void Trigger_GetReferencedItems_EmitsOnTableBeforeBodyItems()
    {
        var trigger = new Trigger(
            new ObjectIdentifier("mytrigger", s_schema),
            new ObjectIdentifier("mytable", s_schema),
            MakeSelect("anothertable"));

        List<ItemRef> items = [.. trigger.GetReferencedItems(CreateManager())];

        Assert.Equal(2, items.Count);
        Assert.Equal(ItemType.Table, items[0].Type);
        Assert.Equal("myschema.mytable", N(items[0]));
        Assert.Equal(ItemType.Table, items[1].Type);
        Assert.Equal("anothertable", N(items[1]));
    }
}
