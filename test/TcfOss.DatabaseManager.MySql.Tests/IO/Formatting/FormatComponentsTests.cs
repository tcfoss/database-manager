namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatComponentsTests
{
    [Theory]
    [InlineData("CALL CONCAT('a', 'b')", "CALL CONCAT('a', 'b');", Label = "Call built-in function")]
    [InlineData("CALL `myfunc`('a', 'b')", "CALL `myfunc`('a', 'b');", Label = "Call quoted user-defined function")]
    [InlineData("CALL myfunc('a', 'b')", "CALL `myfunc`('a', 'b');", Label = "Call user-defined function")]
    [InlineData("CALL schema1.myfunc('a', 'b')", "CALL `schema1`.`myfunc`('a', 'b');", Label = "Call user-defined function with schema")]
    public void CallFunction(string input, string expected)
    {
        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(input);
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData("CALL func(x = 1)", "CALL `func`(`x` = 1);", Label = "Single named arg with = operator")]
    [InlineData("CALL func(x = 1, y = 2)", "CALL `func`(`x` = 1, `y` = 2);", Label = "Multiple named args with = operator")]
    [InlineData("CALL func(x := 1)", "CALL `func`(`x` := 1);", Label = "Named arg with := operator")]
    public void FunctionArgument_Named_FormatSql(string input, string expected)
    {
        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(input);
            Assert.Equal(expected, actual);
        }
    }
}
