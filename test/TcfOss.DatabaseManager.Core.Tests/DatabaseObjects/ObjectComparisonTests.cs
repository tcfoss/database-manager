using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Tests.DatabaseObjects;

public class ObjectComparisonTests
{
    [Fact]
    public void Trigger_Comparison()
    {
        var trigger = new Trigger(
            new ObjectIdentifier("mytrigger", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            new ObjectIdentifier("mytable", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            new SimpleSelect([new SimpleSelectItem.Wildcard()])
            {
                From = [new TableWithJoins(new TableFactor.Table(new ObjectName([new Identifier("mytable")])))]
            }
        )
        {
            RawBodyText = "RAW BODY TEXT 1"
        };

        Trigger? otherTrigger = trigger with { RawBodyText = "RAW BODY TEXT 2" };

        Assert.Equal(trigger, otherTrigger);
        Assert.True(trigger.Equals(otherTrigger));
        Assert.Equal(trigger.GetHashCode(), otherTrigger.GetHashCode());

        otherTrigger = trigger with { Name = new ObjectIdentifier("mytrigger_different", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))) };

        Assert.NotEqual(trigger, otherTrigger);
        Assert.False(trigger.Equals(otherTrigger));
        Assert.NotEqual(trigger.GetHashCode(), otherTrigger.GetHashCode());

        otherTrigger = null;
        Assert.NotEqual(trigger, otherTrigger);
        Assert.False(trigger.Equals(otherTrigger));
        Assert.NotEqual(trigger.GetHashCode(), otherTrigger?.GetHashCode());
    }

    [Fact]
    public void FunctionComparison()
    {
        var function = new StoredFunction(new ObjectIdentifier("myfunction", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            [
                new RoutineParameter.Directed(new Identifier("param1"), new DataType.Int(), RoutineParameterDirection.In),
            ],
            new DataType.Int(),
            new SimpleSelect([new SimpleSelectItem.Wildcard()])
            {
                From = [new TableWithJoins(new TableFactor.Table(new ObjectName([new Identifier("mytable")])))]
            }
        )
        {
            RawBodyText = "RAW BODY TEXT 1"
        };

        StoredFunction? otherFunction = function with { RawBodyText = "RAW BODY TEXT 2" };

        Assert.Equal(function, otherFunction);
        Assert.True(function.Equals(otherFunction));
        Assert.Equal(function.GetHashCode(), otherFunction.GetHashCode());

        otherFunction = function with
        {
            Parameters =
            [
                new RoutineParameter.Directed(new Identifier("param1_different"), new DataType.Int(), RoutineParameterDirection.In),
            ]
        };

        Assert.NotEqual(function, otherFunction);
        Assert.False(function.Equals(otherFunction));
        Assert.NotEqual(function.GetHashCode(), otherFunction.GetHashCode());

        otherFunction = null;
        Assert.NotEqual(function, otherFunction);
        Assert.False(function.Equals(otherFunction));
        Assert.NotEqual(function.GetHashCode(), otherFunction?.GetHashCode());
    }

    [Fact]
    public void ProcedureComparison()
    {
        var procedure = new StoredProcedure(new ObjectIdentifier("myprocedure", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            [
                new RoutineParameter.Directed(new Identifier("param1"), new DataType.Int(), RoutineParameterDirection.In),
            ],
            new SimpleSelect([new SimpleSelectItem.Wildcard()])
            {
                From = [new TableWithJoins(new TableFactor.Table(new ObjectName(new Identifier("mytable"))))]
            }
        )
        {
            RawBodyText = "RAW BODY TEXT 1"
        };

        StoredProcedure? otherProcedure = procedure with { RawBodyText = "RAW BODY TEXT 2" };

        Assert.Equal(procedure, otherProcedure);
        Assert.True(procedure.Equals(otherProcedure));
        Assert.Equal(procedure.GetHashCode(), otherProcedure.GetHashCode());

        otherProcedure = procedure with
        {
            Parameters =
            [
                new RoutineParameter.Directed(new Identifier("param1_different"), new DataType.Int(), RoutineParameterDirection.In),
            ]
        };

        Assert.NotEqual(procedure, otherProcedure);
        Assert.False(procedure.Equals(otherProcedure));
        Assert.NotEqual(procedure.GetHashCode(), otherProcedure.GetHashCode());

        otherProcedure = null;
        Assert.NotEqual(procedure, otherProcedure);
        Assert.False(procedure.Equals(otherProcedure));
        Assert.NotEqual(procedure.GetHashCode(), otherProcedure?.GetHashCode());
    }
}
