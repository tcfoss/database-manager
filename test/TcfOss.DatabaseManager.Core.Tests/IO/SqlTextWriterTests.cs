using System.Text;
using FluentAssertions;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Tests.IO;

public class SqlTextWriterTests
{
    private static FormatManager GetFormatManager()
    {
        var functionNameProvider = new FunctionNameProvider();
        return new FormatManager()
        {
            Formatting = new Core.Configuration.FormattingSettings(),
            FunctionNameProvider = functionNameProvider,
            ComponentNormalizer = new ComponentNormalizer(QuoteStyle.Ansi, functionNameProvider),
            Indenter = new Indenter(),
        };
    }

    [Fact]
    public void RemoveChar_No_Problems_On_Empty()
    {
        var writer = new SqlTextWriter(new StringBuilder());
        writer.RemoveChar(';');

        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void RemoveChar_RemovesTrailingCharAndFollowingWhitespace()
    {
        var writer = new SqlTextWriter(new StringBuilder(" ;  "));
        writer.RemoveChar(';');

        Assert.Equal(" ", writer.ToString());
    }

    [Fact]
    public void RemoveChar_NoEffectCharNotFound()
    {
        var writer = new SqlTextWriter(new StringBuilder("SELECT * FROM table1;"));
        writer.RemoveChar(',');

        Assert.Equal("SELECT * FROM table1;", writer.ToString());
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\r\n\t ")]
    public void RemoveChar_AllWhitespace_IsUnchanged(string text)
    {
        var writer = new SqlTextWriter(new StringBuilder(text));

        writer.RemoveChar(';');

        Assert.Equal(text, writer.ToString());
    }

    [Theory]
    [InlineData("", false, 0)]
    [InlineData("SELECT", false, 6)]
    [InlineData("SELECT\n", true, 0)]
    [InlineData("SELECT\r\nabc", false, 3)]
    public void LineState_ReflectsCurrentBuffer(string text, bool endsWithNewline, int lineLength)
    {
        var writer = new SqlTextWriter(new StringBuilder(text));

        Assert.Equal(endsWithNewline, writer.EndWithNewLine());
        Assert.Equal(lineLength, writer.GetCurrentLineLength());
    }

    [Fact]
    public void Indenter_IsCreatedOnceAndReused()
    {
        var writer = new SqlTextWriter(new StringBuilder());

        Indenter indenter = writer.Indenter;

        Assert.Same(indenter, writer.Indenter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormatDelimited_Array_WritesItems(bool separateLines)
    {
        ObjectName[] items = [new(new Identifier("table1")), new(new Identifier("table2"))];
        var writer = new SqlTextWriter(new StringBuilder());

        if (separateLines)
        {
            writer.FormatDelimitedLines(items, GetFormatManager());
        }
        else
        {
            writer.FormatDelimited(items, GetFormatManager());
        }

        string separator = separateLines ? "," + Environment.NewLine : ", ";
        Assert.Equal($"\"table1\"{separator}\"table2\"", writer.ToString());
    }

    [Theory]
    [InlineData(false, 1, true, "    ")]
    [InlineData(false, 5, true, "        ")]
    [InlineData(false, 1, false, "")]
    [InlineData(false, 7, false, "    ")]
    [InlineData(true, 1, true, "\t")]
    [InlineData(true, 5, true, "\t\t")]
    [InlineData(true, 1, false, "")]
    [InlineData(true, 7, false, "\t")]
    public void Indenter_PartialIndent_RoundsToTabStop(bool preferTabs, int initialSpaces, bool increase, string expected)
    {
        var indenter = new Indenter(4, preferTabs);
        indenter.ResetIndent(initialSpaces);

        if (increase)
        {
            indenter.IncreaseIndent();
        }
        else
        {
            indenter.DecreaseIndent();
        }

        Assert.Equal(expected, indenter.IndentString);
    }

    [Fact]
    public void TrimEnd_RemovesTrailingWhitespace()
    {
        var writer = new SqlTextWriter(new StringBuilder("SELECT * FROM table1;   \r\n\t "));
        writer.TrimEnd();

        Assert.Equal("SELECT * FROM table1;", writer.ToString());
    }

    [Fact]
    public void TrimEnd_NoEffectOnNoTrailingWhitespace()
    {
        var writer = new SqlTextWriter(new StringBuilder("SELECT * FROM table1;"));
        writer.TrimEnd();

        Assert.Equal("SELECT * FROM table1;", writer.ToString());
    }

    [Fact]
    public void TrimEnd_NoProblemsOnEmpty()
    {
        var writer = new SqlTextWriter(new StringBuilder());
        writer.TrimEnd();

        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void TrimEnd_EmptyStringOnAllWhitespace()
    {
        var writer = new SqlTextWriter(new StringBuilder("   \r\n\t "));
        writer.TrimEnd();

        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void FormatDelimited_NoProblemsOnNull()
    {
        var writer = new SqlTextWriter(new StringBuilder());
        writer.FormatDelimited<Select>(null, GetFormatManager());

        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void FormatDelimited_WritesDelimitedItems()
    {
        var items = new List<ObjectName>
        {
            new(new Identifier("table1")),
            new(new Identifier("table2")),
            new(new Identifier("table3")),
        };

        var writer = new SqlTextWriter(new StringBuilder());
        writer.FormatDelimited(items, GetFormatManager());

        var actual = writer.ToString();
        var expected = "\"table1\", \"table2\", \"table3\"";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatDelimitedLines_NoProblemsOnNull()
    {
        var writer = new SqlTextWriter(new StringBuilder());
        writer.FormatDelimitedLines<Select>(null, GetFormatManager());

        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void FormatDelimitedLines_WritesDelimitedItemsOnSeparateLines()
    {
        var items = new List<ObjectName>
        {
            new(new Identifier("table1")),
            new(new Identifier("table2")),
            new(new Identifier("table3")),
        };

        var writer = new SqlTextWriter(new StringBuilder());
        writer.FormatDelimitedLines(items, GetFormatManager());

        var sep = Environment.NewLine;
        var actual = writer.ToString();
        var expected = $"\"table1\",{sep}\"table2\",{sep}\"table3\"";
        Assert.Equal(expected, actual);
    }
}
