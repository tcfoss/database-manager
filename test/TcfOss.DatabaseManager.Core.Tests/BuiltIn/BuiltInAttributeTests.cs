using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.IO;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<DateTimeUnit>), typeof(DateTimeUnit))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<BinaryOperator>), typeof(BinaryOperator))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<UnaryOperator>), typeof(UnaryOperator))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.StringEnumTestHelpers.StringEnumSerializer<AggregateQuantifier>), typeof(AggregateQuantifier))]

namespace TcfOss.DatabaseManager.Core.Tests.BuiltIn;

public class BuiltInAttributeTests
{
    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<DateTimeUnit>))]
    public void Test_Date_Time_Unit_From_Text(string text, DateTimeUnit expected)
    {
        var result = DateTimeUnit.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Date_Time_Unit_Throws()
    {
        Assert.Throws<ArgumentException>(() => DateTimeUnit.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<BinaryOperator>))]
    public void Test_Binary_Operator_From_Text(string text, BinaryOperator expected)
    {
        var result = BinaryOperator.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Binary_Operator_Throws()
    {
        Assert.Throws<ArgumentException>(() => BinaryOperator.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<UnaryOperator>))]
    public void Test_Unary_Operator_From_Text(string text, UnaryOperator expected)
    {
        var result = UnaryOperator.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Unary_Operator_Throws()
    {
        Assert.Throws<ArgumentException>(() => UnaryOperator.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<AggregateQuantifier>))]
    public void Test_Aggregate_Quantifier_From_Text(string text, AggregateQuantifier expected)
    {
        var result = AggregateQuantifier.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void Test_Unknown_Aggregate_Quantifier_Throws()
    {
        Assert.Throws<ArgumentException>(() => AggregateQuantifier.Parse("UNKNOWN"));
    }
}
