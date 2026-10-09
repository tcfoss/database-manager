using System.Text;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

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
        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CALL func(x = 1)", "CALL `func`(`x` = 1);", Label = "Single named arg with = operator")]
    [InlineData("CALL func(x = 1, y = 2)", "CALL `func`(`x` = 1, `y` = 2);", Label = "Multiple named args with = operator")]
    [InlineData("CALL func(x := 1)", "CALL `func`(`x` := 1);", Label = "Named arg with := operator")]
    public void FunctionArgument_Named_FormatSql(string input, string expected)
    {
        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("BTREE")]
    [InlineData("HASH")]
    public void UniqueIndex_PreservesIndexMethod(string method)
    {
        string input = $"CREATE TABLE t (id INT, INDEX ix UNIQUE USING {method} (id))";
        var parser = new TextParser(new MyLexer(), new MyParser());
        var table = Assert.IsType<CreateTable>(Assert.Single(parser.ParseText(input)));
        var index = Assert.IsType<StatementTableConstraint.UniqueIndex>(Assert.Single(table.Constraints));
        using var writer = new SqlTextWriter(new StringBuilder());
        index.ToSql(writer);
        Assert.Equal($"INDEX ix UNIQUE USING {method} (id)", writer.ToString());

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(table);
        string expected = $"""
        CREATE TABLE `t`
        (
            `id` INT,
            INDEX `ix` UNIQUE USING {method} (`id`)
        );
        """;
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }
}
