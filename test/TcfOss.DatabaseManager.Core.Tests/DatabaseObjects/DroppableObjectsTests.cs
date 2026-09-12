using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Tests.DatabaseObjects;

public class DroppableObjectsTests
{
    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<DroppableObject>))]
    public void Test_Droppable_Object_ToSql(string text, DroppableObject droppable)
    {
        var result = DroppableObject.Parse(text);
        Assert.Equal(droppable.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Droppable_Object_Throws()
    {
        Assert.Throws<ArgumentException>(() => DroppableObject.Parse("UNKNOWN"));
    }
}
