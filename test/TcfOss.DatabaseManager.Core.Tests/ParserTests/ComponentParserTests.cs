using System.Collections;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using Xunit.Sdk;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

[assembly: RegisterXunitSerializer(typeof(ComponentParserTests.AccountSerializer), typeof(Account))]
[assembly: RegisterXunitSerializer(typeof(StringEnumTestHelpers.StringEnumSerializer<Direction>), typeof(Direction))]

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

// ReSharper disable ClassNeverInstantiated.Global
public class ComponentParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("dog")]
    [InlineData("`dog`")]
    [InlineData("[dog]")]
    [InlineData("\"dog\"")]
    [InlineData("_dog")]
    [InlineData("@dog")]
    [InlineData("`2`")]
    public static void ParseIdentifier_TextMatch(string identifierSql)
    {
        var parser = new Parser();

        var (expected, actual) = GetExpectedActual(parser.ComponentParser.ParseIdentifier, identifierSql);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("dog")]
    [InlineData("`dog`")]
    [InlineData("[dog]")]
    [InlineData("\"dog\"")]
    [InlineData("_dog")]
    [InlineData("'dog'")]
    [InlineData("`2`")]
    public static void ParseExtendedIdentifier_TextMatch(string identifierSql)
    {
        var (expected, actual) = GetExpectedActual(ComponentParser.ParseExtendedIdentifier, identifierSql);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ParseIdentifier_String_To_Default_Quoted_Identifier()
    {
        var parser = new Parser();

        var actualTokens = new GenericLexer().Tokenize("'dog'");
        var state = new ParserState([.. actualTokens]);
        var actual = parser.ComponentParser.ParseIdentifier(state);
        var expected = new Identifier("dog", parser.DefaultQuoteStyle);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("3.2")]
    [InlineData("*")]
    [InlineData("<>")]
    [InlineData("(")]
    [InlineData(".")]
    public static void ParseIdentifier_Invalid_Values_Fail(string badSql)
    {
        var parser = new Parser();

        var tokens = new GenericLexer().Tokenize(badSql);
        var state = new ParserState([.. tokens]);

        Assert.Throws<ParseException.ExpectedButFound>(() => parser.ComponentParser.ParseIdentifier(state));
    }

    [Theory]
    [InlineData("aardvark")]
    [InlineData("aardvark.`bobcat`")]
    [InlineData("aardvark.`bobcat`.\"cat\"")]
    [InlineData("aardvark.`bobcat`.\"cat\".[dog]")]
    public static void ParseObjectName_TextMatch(string nameSql)
    {
        var parser = new Parser();

        var (expected, actual) = GetExpectedActual(parser.ComponentParser.ParseObjectName, nameSql);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("3.2")]
    [InlineData("*")]
    [InlineData("<>")]
    [InlineData("(")]
    [InlineData(".")]
    public static void ParseObjectName_Invalid_Values_Fail(string badSql)
    {
        var parser = new Parser();

        var tokens = new GenericLexer().Tokenize(badSql);
        var state = new ParserState([.. tokens]);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ComponentParser.ParseObjectName(state));
        var expectedTemplate = "Expected {0}. Found {1}.";
        var expected = string.Format(expectedTemplate, "identifier".Italic(), badSql);
        Assert.Equal(expected, exception.Message);
    }

    [Theory]
    [InlineData("(`aardvark`)")]
    [InlineData("(`aardvark`, \"bobcat\")")]
    [InlineData("(`aardvark`, \"bobcat\", [cat])")]
    [InlineData("(`aardvark`, \"bobcat\", [cat], dog)")]
    public static void ParseParenthesizedIdentifierList_TextMatch(string sql)
    {
        var parser = new Parser();

        var (expected, actual) = GetExpectedActual(s => parser.ComponentParser.ParseParenthisizedIdentifierList(s, false), sql);
        Assert.Equal(expected, $"({actual})");
    }

    [Fact]
    public static void ParseParenthesizedIdentifierList_Empty_Allowed()
    {
        var state = GetState("()");
        var parser = new Parser();

        var actual = parser.ComponentParser.ParseParenthesizedKeyPartList(state, true);
        Assert.NotNull(actual);
        Assert.Empty(actual);
    }

    [Fact]
    public static void ParseParenthesizedIdentifierList_Empty_Forbidden()
    {
        var state = GetState("()");
        var parser = new Parser();

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ComponentParser.ParseParenthesizedKeyPartList(state, false));
        var expectedTemplate = "Expected {0}. Found {1}.";
        var expected = string.Format(expectedTemplate, "comma_separated_list".Italic(), ")");
        Assert.Equal(expected, exception.Message);
    }

    [Theory]
    [InlineData("(`aardvark`(3))")]
    [InlineData("(`aardvark`, \"bobcat\"(4) DESC)")]
    [InlineData("(`aardvark`(5) ASC, \"bobcat\"(4) DESC, [cat] DESC)")]
    [InlineData("(`aardvark`, \"bobcat\", [cat], dog)")]
    public static void ParseParenthesizedKeyPartList_TextMatch(string sql)
    {
        var parser = new Parser();

        var (expected, actual) = GetExpectedActual(s => parser.ComponentParser.ParseParenthesizedKeyPartList(s, false), sql);
        Assert.Equal(expected, $"({actual})");
    }

    [Theory]
    [ClassData(typeof(DirectionTestData))]
    public static void ParseDirection_Values_Match(string sql, Direction? expected)
    {
        var state = GetState(sql);

        var actual = ComponentParser.ParseDirection(state);
        Assert.Equal(expected, actual);
    }

    private class DirectionTestData : IEnumerable<TheoryDataRow<string, Direction?>>
    {
        public IEnumerator<TheoryDataRow<string, Direction?>> GetEnumerator()
        {
            yield return new("ASC", Direction.Ascending);
            yield return new("DESC", Direction.Descending);
            yield return new("x", null);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    [Theory]
    [ClassData(typeof(SetQuantifierTestData))]
    public static void ParseSetQuantifier_Values_Match(string sql, SetQuantifier? expected)
    {
        var state = GetState(sql);

        var actual = ComponentParser.ParseSetQuantifier(state);
        Assert.Equal(expected, actual);
    }

    private class SetQuantifierTestData : StringEnumTestHelpers.StringEnumTestData<SetQuantifier>
    {
        protected override IEnumerable<TheoryDataRow<string, SetQuantifier>> ExtraRows => [
            new("X", null!)
        ];
    }

    [Theory]
    [ClassData(typeof(CreateOrLabelTestData))]
    public static void ParseCreateOrLabel_Values_Match(string sql, CreateOrLabel? expected)
    {
        var state = GetState(sql);

        var actual = ComponentParser.ParseCreateOrLabel(state);
        Assert.Equal(expected, actual);
    }

    private class CreateOrLabelTestData : StringEnumTestHelpers.StringEnumTestData<CreateOrLabel>
    {
        protected override IEnumerable<TheoryDataRow<string, CreateOrLabel>> ExtraRows => [
            new("X", null!)
        ];
    }

    [Theory]
    [InlineData("col1")]
    [InlineData("col2 ASC")]
    [InlineData("col3 DESC")]
    [InlineData("COUNT(*) ASC")]
    [InlineData("COUNT(1) DESC")]
    public static void ParseOrderBy_TextMatch(string sql)
    {
        var parser = new Parser();

        var (expected, actual) = GetExpectedActual(parser.ComponentParser.ParseOrderBy, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("DISTINCT")]
    [InlineData("DISTINCT ON ()")]
    [InlineData("DISTINCT ON (col1)")]
    [InlineData("DISTINCT ON (col1, COUNT(*))")]
    [InlineData("DISTINCTROW")]
    public static void ParseAllOrDistinct_TextMatch(string sql)
    {
        var parser = new Parser();

        var (expected, actual) = GetExpectedActual(parser.ComponentParser.ParseAllOrDistinct, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ALL", "ALL")]
    [InlineData("DISTINCT", "DISTINCT")]
    [InlineData("CAT", null)]
    public static void ParseDuplicateTreatment_TextMatch(string sql, string? expected)
    {
        var state = GetState(sql);
        var actual = ComponentParser.ParseOptionalDuplicateTreatment(state)?.ToSql();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ParseAllOrDistinct_Complex_Distinct_On()
    {
        var parser = new Parser();
        var state = GetState("DISTINCT ON (col1, COUNT(*))");

        var actual = parser.ComponentParser.ParseAllOrDistinct(state);
        var expected = new DistinctFilter.On([
            new SingleIdentifier(new Identifier("col1")),
            new FunctionCall(new ObjectName(new Identifier("COUNT")))
            {
                Arguments = new FunctionArguments.List([
                    new FunctionArgument.Unnamed(new FunctionArgumentExpression.Wildcard())
                ])
            }
        ]);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [ClassData(typeof(AccountTestData))]
    public static void ParseAccount(string sql, Account expected)
    {
        var state = GetState(sql);

        var actual = ComponentParser.ParseAccount(state);

        Assert.Equal(expected, actual);
    }

    private class AccountTestData : IEnumerable<TheoryDataRow<string, Account>>
    {
        public IEnumerator<TheoryDataRow<string, Account>> GetEnumerator()
        {
            yield return new("CURRENT_USER", new Account.CurrentUser());
            yield return new("CURRENT_ROLE", new Account.CurrentRole());
            yield return new("SESSION_USER", new Account.SessionUser());
            yield return new("'username'", new Account.Identity(new ExtendedIdentifier("username", ExtendedQuoteStyle.SingleQuote)));
            yield return new("'username'@'hostname'", new Account.IdentityWithHost(new ExtendedIdentifier("username", ExtendedQuoteStyle.SingleQuote), new ExtendedIdentifier("hostname", ExtendedQuoteStyle.SingleQuote)));
            yield return new("\"username\"", new Account.Identity(new Identifier("username", QuoteStyle.Ansi)));
            yield return new("[username]@`hostname`", new Account.IdentityWithHost(new Identifier("username", QuoteStyle.Brackets), new Identifier("hostname", QuoteStyle.Backticks)));

        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    public class AccountSerializer : IXunitSerializer
    {
        public object Deserialize(Type type, string serializedValue)
        {
            var state = GetState(serializedValue);
            return ComponentParser.ParseAccount(state);
        }

        public bool IsSerializable(Type type, object? value, out string failureReason)
        {
            var account = value as Account;
            if (account == null)
            {
                failureReason = "Not an account";
                return false;
            }
            failureReason = "";
            return true;
        }

        public string Serialize(object value)
        {
            var account = value as Account;
            if (account == null)
            {
                return "";
            }
            return account.ToSql();
        }
    }

    [Theory]
    [InlineData("DEFINER = CURRENT_USER")]
    [InlineData("DEFINER = CURRENT_ROLE")]
    [InlineData("DEFINER = SESSION_USER")]
    [InlineData("DEFINER = `root`")]
    [InlineData("DEFINER = 'root'@[localhost]")]
    [InlineData("SELECT * FROM my_table")]
    public static void ParseDefinerOptional_Text_Equal(string sql)
    {
        if (sql.StartsWith("DEFINER", StringComparison.InvariantCultureIgnoreCase))
        {
            var (expected, actual) = GetExpectedActual(ComponentParser.ParseOptionalDefiner, sql);
            Assert.Equal(expected, actual);
        }
        else
        {
            var state = GetState(sql);
            Assert.Null(ComponentParser.ParseOptionalDefiner(state));
        }
    }
}
