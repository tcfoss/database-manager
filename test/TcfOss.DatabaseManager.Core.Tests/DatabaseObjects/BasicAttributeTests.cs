using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.IO;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<Direction>), typeof(Direction))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<EventEnabledStatus>), typeof(EventEnabledStatus))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<IndexMethod>), typeof(IndexMethod))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<ReferentialAction>), typeof(ReferentialAction))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<RoutineParameterDirection>), typeof(RoutineParameterDirection))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<SecurityContext>), typeof(SecurityContext))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<SqlDataRelation>), typeof(SqlDataRelation))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<TriggerEvent>), typeof(TriggerEvent))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<TriggerOrderPosition>), typeof(TriggerOrderPosition))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<TriggerTime>), typeof(TriggerTime))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<ViewAlgorithm>), typeof(ViewAlgorithm))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<ViewCheckOption>), typeof(ViewCheckOption))]

namespace TcfOss.DatabaseManager.Core.Tests.DatabaseObjects;

public class BasicAttributeTests
{

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<Direction>))]
    public void Test_Direction_From_Text(string text, Direction expected)
    {
        var result = Direction.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Direction_Throws()
    {
        Assert.Throws<ArgumentException>(() => Direction.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<EventEnabledStatus>))]
    public void Test_Event_Enabled_Status_From_Text(string text, EventEnabledStatus expected)
    {
        var result = EventEnabledStatus.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Event_Enabled_Status_Throws()
    {
        Assert.Throws<ArgumentException>(() => EventEnabledStatus.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<IndexMethod>))]
    public void Test_Index_Method_From_Text(string text, IndexMethod expected)
    {
        var result = IndexMethod.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Index_Method_Throws()
    {
        Assert.Throws<ArgumentException>(() => IndexMethod.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<ReferentialAction>))]
    public void Test_Referential_Action_From_Text(string text, ReferentialAction expected)
    {
        var result = ReferentialAction.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Referential_Action_Throws()
    {
        Assert.Throws<ArgumentException>(() => ReferentialAction.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(RoutineParameterDirectionTestData))]
    public void Test_Routine_Parameter_Direction_From_Text(string text, RoutineParameterDirection expected)
    {
        var result = RoutineParameterDirection.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Routine_Parameter_Direction_Throws()
    {
        Assert.Throws<ArgumentException>(() => RoutineParameterDirection.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<SecurityContext>))]
    public void Test_Security_Context_From_Text(string text, SecurityContext expected)
    {
        var result = SecurityContext.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Security_Context_Throws()
    {
        Assert.Throws<ArgumentException>(() => SecurityContext.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<SqlDataRelation>))]
    public void Test_Sql_Data_Relation_From_Text(string text, SqlDataRelation expected)
    {
        var result = SqlDataRelation.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Sql_Data_Relation_Throws()
    {
        Assert.Throws<ArgumentException>(() => SqlDataRelation.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<TriggerEvent>))]
    public void Test_Trigger_Event_From_Text(string text, TriggerEvent expected)
    {
        var result = TriggerEvent.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Trigger_Event_Throws()
    {
        Assert.Throws<ArgumentException>(() => TriggerEvent.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<TriggerOrderPosition>))]
    public void Test_Trigger_Order_Position_From_Text(string text, TriggerOrderPosition expected)
    {
        var result = TriggerOrderPosition.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Trigger_Order_Position_Throws()
    {
        Assert.Throws<ArgumentException>(() => TriggerOrderPosition.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<TriggerTime>))]
    public void Test_Trigger_Time_From_Text(string text, TriggerTime expected)
    {
        var result = TriggerTime.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Trigger_Time_Throws()
    {
        Assert.Throws<ArgumentException>(() => TriggerTime.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<ViewAlgorithm>))]
    public void Test_View_Algorithm_From_Text(string text, ViewAlgorithm expected)
    {
        var result = ViewAlgorithm.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_View_Algorithm_Throws()
    {
        Assert.Throws<ArgumentException>(() => ViewAlgorithm.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<ViewCheckOption>))]
    public void Test_View_Check_Option_From_Text(string text, ViewCheckOption expected)
    {
        var result = ViewCheckOption.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_View_Check_Option_Throws()
    {
        Assert.Throws<ArgumentException>(() => ViewCheckOption.Parse("UNKNOWN"));
    }


    private class RoutineParameterDirectionTestData : StringEnumTestHelpers.StringEnumTestData<RoutineParameterDirection> //, IEnumerable<TheoryDataRow<string, RoutineParameterDirection>>
    {
        protected override IEnumerable<TheoryDataRow<string, RoutineParameterDirection>> ExtraRows =>
        [
            new("IN OUT", RoutineParameterDirection.InOut)
        ];
    }
}
