using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseObjects;

public class MyObjectComparisonTests
{
    private static Select GetSelectStatement(string tableName = "mytable")
    {
        return new Select(
            new SelectBody.SimpleSelectQuery(
                new SimpleSelect([new SimpleSelectItem.Wildcard()])
                {
                    From = [new TableWithJoins(new TableFactor.Table(new ObjectName(new Identifier(tableName))))]
                }));
    }

    [Fact]
    public void ViewComparison()
    {
        var view = new MyView(
            new ObjectIdentifier("myview", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            GetSelectStatement()
        )
        {
            Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("user"), new ExtendedIdentifier("host"))),
            SecurityContext = SecurityContext.Invoker,
            Algorithm = ViewAlgorithm.Merge,
            CheckOption = ViewCheckOption.Cascaded,
            RawBodyText = "RAW BODY TEXT 1"
        };

        // Change RawBodyText
        MyView? otherView = view with { RawBodyText = "RAW BODY TEXT 2" };

        Assert.Equal(view, otherView);
        Assert.True(view.Equals(otherView));
        Assert.Equal(view.GetHashCode(), otherView.GetHashCode());

        otherView = view with { Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("otheruser"), new ExtendedIdentifier("host"))) };

        Assert.NotEqual(view, otherView);
        Assert.False(view.Equals(otherView));
        Assert.NotEqual(view.GetHashCode(), otherView.GetHashCode());

        // Change SecurityContext
        otherView = view with { SecurityContext = SecurityContext.Definer };

        Assert.NotEqual(view, otherView);
        Assert.False(view.Equals(otherView));
        Assert.NotEqual(view.GetHashCode(), otherView.GetHashCode());

        // Change Algorithm
        otherView = view with { Algorithm = ViewAlgorithm.TempTable };

        Assert.NotEqual(view, otherView);
        Assert.False(view.Equals(otherView));
        Assert.NotEqual(view.GetHashCode(), otherView.GetHashCode());

        // Change Body
        otherView = view with { Body = GetSelectStatement("othertable") };

        Assert.NotEqual(view, otherView);
        Assert.False(view.Equals(otherView));
        Assert.NotEqual(view.GetHashCode(), otherView.GetHashCode());

        // Compare to Null
        otherView = null;
        Assert.NotEqual(view, otherView);
        Assert.False(view.Equals(otherView));
        Assert.NotEqual(view.GetHashCode(), otherView?.GetHashCode());
    }

    [Fact]
    public void TriggerComparison()
    {
        var trigger = new MyTrigger(
            new ObjectIdentifier("mytrigger", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            new ObjectIdentifier("mytable", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            TriggerTime.After,
            TriggerEvent.Insert,
            GetSelectStatement()
        )
        {
            Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("user"), new ExtendedIdentifier("host"))),
            Order = 1,
            RawBodyText = "RAW BODY TEXT 1"
        };

        // Change RawBodyText
        MyTrigger otherTrigger = trigger with { RawBodyText = "RAW BODY TEXT 2" };
        Assert.Equal(trigger, otherTrigger);
        Assert.True(trigger.Equals(otherTrigger));
        Assert.Equal(trigger.GetHashCode(), otherTrigger.GetHashCode());

        // Change TriggerTime
        otherTrigger = trigger with { TriggerTime = TriggerTime.Before };
        Assert.NotEqual(trigger, otherTrigger);
        Assert.False(trigger.Equals(otherTrigger));
        Assert.NotEqual(trigger.GetHashCode(), otherTrigger.GetHashCode());

        // Change TriggerEvent
        otherTrigger = trigger with { TriggerEvent = TriggerEvent.Update };
        Assert.NotEqual(trigger, otherTrigger);
        Assert.False(trigger.Equals(otherTrigger));
        Assert.NotEqual(trigger.GetHashCode(), otherTrigger.GetHashCode());

        // Change order
        otherTrigger = trigger with { Order = 2 };
        Assert.NotEqual(trigger, otherTrigger);
        Assert.False(trigger.Equals(otherTrigger));
        Assert.NotEqual(trigger.GetHashCode(), otherTrigger.GetHashCode());

        // Change Body
        otherTrigger = trigger with { Body = GetSelectStatement("othertable") };
        Assert.NotEqual(trigger, otherTrigger);
        Assert.False(trigger.Equals(otherTrigger));
        Assert.NotEqual(trigger.GetHashCode(), otherTrigger.GetHashCode());

        // Change Definer
        otherTrigger = trigger with { Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("otheruser"), new ExtendedIdentifier("host"))) };
        Assert.NotEqual(trigger, otherTrigger);
        Assert.False(trigger.Equals(otherTrigger));
        Assert.NotEqual(trigger.GetHashCode(), otherTrigger.GetHashCode());
    }

    [Fact]
    public void ProcedureComparison()
    {
        var procedure = new MyStoredProcedure(new ObjectIdentifier("myprocedure", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            [
                new RoutineParameter.Directed(new Identifier("param1"), new DataType.Int(), RoutineParameterDirection.In),
            ],
            GetSelectStatement()
        )
        {
            Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("user"), new ExtendedIdentifier("host"))),
            SecurityContext = SecurityContext.Invoker,
            Comment = new Comment("This is a comment"),
            Deterministic = true,
            SqlDataRelation = SqlDataRelation.ContainsSql,
            RawBodyText = "RAW BODY TEXT 1"
        };

        // Change RawBodyText
        MyStoredProcedure otherProcedure = procedure with { RawBodyText = "RAW BODY TEXT 2" };
        Assert.Equal(procedure, otherProcedure);
        Assert.True(procedure.Equals(otherProcedure));
        Assert.Equal(procedure.GetHashCode(), otherProcedure.GetHashCode());

        // Change Definer
        otherProcedure = procedure with { Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("otheruser"), new ExtendedIdentifier("host"))) };
        Assert.NotEqual(procedure, otherProcedure);
        Assert.False(procedure.Equals(otherProcedure));
        Assert.NotEqual(procedure.GetHashCode(), otherProcedure.GetHashCode());

        // Change SecurityContext
        otherProcedure = procedure with { SecurityContext = SecurityContext.Definer };
        Assert.NotEqual(procedure, otherProcedure);
        Assert.False(procedure.Equals(otherProcedure));
        Assert.NotEqual(procedure.GetHashCode(), otherProcedure.GetHashCode());

        // Change Comment
        otherProcedure = procedure with { Comment = new Comment("Different comment") };
        Assert.NotEqual(procedure, otherProcedure);
        Assert.False(procedure.Equals(otherProcedure));
        Assert.NotEqual(procedure.GetHashCode(), otherProcedure.GetHashCode());
    }

    [Fact]
    public void FunctionComparison()
    {
        var function = new MyStoredFunction(new ObjectIdentifier("myfunction", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            [
                new RoutineParameter.Directed(new Identifier("param1"), new DataType.Int(), RoutineParameterDirection.In),
            ],
            new DataType.Int(),
            GetSelectStatement()
        )
        {
            Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("user"), new ExtendedIdentifier("host"))),
            SecurityContext = SecurityContext.Invoker,
            Comment = new Comment("This is a comment"),
            Deterministic = true,
            SqlDataRelation = SqlDataRelation.ContainsSql,
            RawBodyText = "RAW BODY TEXT 1"
        };

        // Change RawBodyText
        MyStoredFunction otherFunction = function with { RawBodyText = "RAW BODY TEXT 2" };
        Assert.Equal(function, otherFunction);
        Assert.True(function.Equals(otherFunction));
        Assert.Equal(function.GetHashCode(), otherFunction.GetHashCode());

        // Change Definer
        otherFunction = function with { Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("otheruser"), new ExtendedIdentifier("host"))) };
        Assert.NotEqual(function, otherFunction);
        Assert.False(function.Equals(otherFunction));
        Assert.NotEqual(function.GetHashCode(), otherFunction.GetHashCode());

        // Change SecurityContext
        otherFunction = function with { SecurityContext = SecurityContext.Definer };
        Assert.NotEqual(function, otherFunction);
        Assert.False(function.Equals(otherFunction));
        Assert.NotEqual(function.GetHashCode(), otherFunction.GetHashCode());

        // Change Comment
        otherFunction = function with { Comment = new Comment("Different comment") };
        Assert.NotEqual(function, otherFunction);
        Assert.False(function.Equals(otherFunction));
        Assert.NotEqual(function.GetHashCode(), otherFunction.GetHashCode());
    }

    [Fact]
    public void EventComparison()
    {
        var myEvent = new MyEvent(
            new ObjectIdentifier("myevent", new SchemaIdentifier("myschema", new CatalogIdentifier("def"))),
            new EventSchedule.Every(new LiteralValue(new Value.Number("5", false)), DateTimeUnit.Day),
            GetSelectStatement()
        )
        {
            Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("user"), new ExtendedIdentifier("host"))),
            EventEnabledStatus = EventEnabledStatus.Enable,
            OnCompletionPreserve = true,
            Comment = new Comment("This is a comment"),
            RawBodyText = "RAW BODY TEXT 1"
        };

        // Change RawBodyText
        MyEvent otherEvent = myEvent with { RawBodyText = "RAW BODY TEXT 2" };
        Assert.Equal(myEvent, otherEvent);
        Assert.True(myEvent.Equals(otherEvent));
        Assert.Equal(myEvent.GetHashCode(), otherEvent.GetHashCode());

        // Change Definer
        otherEvent = myEvent with { Definer = new Definer(new Account.IdentityWithHost(new ExtendedIdentifier("otheruser"), new ExtendedIdentifier("host"))) };
        Assert.NotEqual(myEvent, otherEvent);
        Assert.False(myEvent.Equals(otherEvent));
        Assert.NotEqual(myEvent.GetHashCode(), otherEvent.GetHashCode());

        // Change EventEnabledStatus
        otherEvent = myEvent with { EventEnabledStatus = EventEnabledStatus.Disable };
        Assert.NotEqual(myEvent, otherEvent);
        Assert.False(myEvent.Equals(otherEvent));
        Assert.NotEqual(myEvent.GetHashCode(), otherEvent.GetHashCode());

        // Change OnCompletionPreserve
        otherEvent = myEvent with { OnCompletionPreserve = false };
        Assert.NotEqual(myEvent, otherEvent);
        Assert.False(myEvent.Equals(otherEvent));
        Assert.NotEqual(myEvent.GetHashCode(), otherEvent.GetHashCode());

        // Change Body
        otherEvent = myEvent with { Body = GetSelectStatement("othertable") };
        Assert.NotEqual(myEvent, otherEvent);
        Assert.False(myEvent.Equals(otherEvent));
        Assert.NotEqual(myEvent.GetHashCode(), otherEvent.GetHashCode());
    }
}
