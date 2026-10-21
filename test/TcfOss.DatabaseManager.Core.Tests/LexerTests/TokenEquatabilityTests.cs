using System.Collections;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.LexerTests.TokenEquatability.MyTerminatorSerializer), typeof(MyTerminator))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.LexerTests.TokenEquatability.NumericLiteralSerializer), typeof(NumericLiteral))]
namespace TcfOss.DatabaseManager.Core.Tests.LexerTests;

public class TokenEquatability
{
    [Theory]
    [ClassData(typeof(TokenTestData))]
    public void TestTokenEquality(Token token1, Token token2)
    {
        SqlValueList<Token> tokens = [new Eof() { Location = new Lexing.Location() }];
        HashSet<Token> tokenSet = [.. tokens];

        Assert.Equal(token1, token2);

        tokens.Add(token1);
        tokens.Add(token2);

        var added = tokenSet.Add(token1);
        Assert.True(added);
        added = tokenSet.Add(token2);
        Assert.False(added);

        Assert.Equal(tokens.Count, tokenSet.Count + 1);
    }

    [Fact]
    public void NumericLiteral_ValuesDifferent_BothNotLong_Different()
    {
        var token1 = new NumericLiteral("123") { Location = new Lexing.Location() };
        var token2 = new NumericLiteral("456") { Location = new Lexing.Location() };

        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void NumericLiteral_ValuesDifferent_BothLong_Different()
    {
        var token1 = new NumericLiteral("123", true) { Location = new Lexing.Location() };
        var token2 = new NumericLiteral("456", true) { Location = new Lexing.Location() };

        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void NumericLiteral_ValuesSame_OneLongOneNotLong_Different()
    {
        var token1 = new NumericLiteral("123", true) { Location = new Lexing.Location() };
        var token2 = new NumericLiteral("123") { Location = new Lexing.Location() };

        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void MyTerminator_Equality()
    {
        var token1 = new MyTerminator("//") { Location = new Lexing.Location(1, 2, 3) };
        var token2 = new MyTerminator("//") { Location = new Lexing.Location(4, 5, 6) };

        Assert.Equal(token1, token2);
        Assert.Equal(token1, (object)token2);
        Assert.Equal(token1, (Token)token2);
        Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
    }

    [Fact]
    public void MyTerminator_Inequality()
    {
        var token1 = new MyTerminator("//") { Location = new Lexing.Location(1, 2, 3) };
        var token2 = new MyTerminator(";;") { Location = new Lexing.Location(4, 5, 6) };

        Assert.NotEqual(token1, token2);
        Assert.NotEqual(token1, (object)token2);
        Assert.NotEqual(token1, (Token)token2);
        Assert.NotEqual(token1.GetHashCode(), token2.GetHashCode());

#pragma warning disable xUnit2000 // Constants and literals should be the expected argument
        Assert.NotEqual(token1, null);
        Assert.False(token1.Equals(null));
#pragma warning restore xUnit2000 // Constants and literals should be the expected argument
    }

    [Fact]
    public void SymbolToken_Equality()
    {
        var token1 = new Asterisk() { Location = new Lexing.Location(1, 2, 3) };
        var token2 = new Asterisk() { Location = new Lexing.Location(4, 5, 6) };

        Assert.Equal(token1, token2);
        Assert.Equal<SymbolToken>(token1, token2);
        Assert.Equal(token1, (object)token2);
        Assert.Equal(token1, (Token)token2);
        Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
    }

    [Fact]
    public void SymbolToken_Inequality()
    {
        var token1 = new Asterisk() { Location = new Lexing.Location(1, 2, 3) };
        var token2 = new Plus() { Location = new Lexing.Location(4, 5, 6) };

        Assert.NotEqual<SymbolToken>(token1, token2);
        Assert.NotEqual(token1, (object)token2);
        Assert.NotEqual(token1, (Token)token2);
        Assert.NotEqual(token1.GetHashCode(), token2.GetHashCode());

#pragma warning disable xUnit2000 // Constants and literals should be the expected argument
        Assert.NotEqual(token1, null);
        Assert.False(token1.Equals(null));
#pragma warning restore xUnit2000 // Constants and literals should be the expected argument
    }

    [Fact]
    public void NonSql_Equality_InlineComment()
    {
        var token1 = new NonSql(NonSqlType.InlineComment, "comment") { Prefix = "--", Location = new Lexing.Location(1, 2, 3) };
        var token2 = new NonSql(NonSqlType.InlineComment, "comment") { Prefix = "--", Location = new Lexing.Location(4, 5, 6) };

        Assert.Equal(token1, token2);
        Assert.Equal(token1, (object)token2);
        Assert.Equal(token1, (Token)token2);
        Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
    }

    [Fact]
    public void NonSql_Inequality_InlineComment_Comment()
    {
        var token1 = new NonSql(NonSqlType.InlineComment, "comment") { Prefix = "--", Location = new Lexing.Location(1, 2, 3) };
        var token2 = new NonSql(NonSqlType.InlineComment, "different") { Prefix = "--", Location = new Lexing.Location(4, 5, 6) };

        Assert.NotEqual(token1, token2);
        Assert.NotEqual(token1, (object)token2);
        Assert.NotEqual(token1, (Token)token2);
        Assert.NotEqual(token1.GetHashCode(), token2.GetHashCode());

        Assert.False(token1.Equals(null));
    }

    [Fact]
    public void NonSql_Inequality_InlineComment_Prefix()
    {
        var token1 = new NonSql(NonSqlType.InlineComment, "comment") { Prefix = "--", Location = new Lexing.Location(1, 2, 3) };
        var token2 = new NonSql(NonSqlType.InlineComment, "comment") { Prefix = "#", Location = new Lexing.Location(4, 5, 6) };

        Assert.NotEqual(token1, token2);
        Assert.NotEqual(token1, (object)token2);
        Assert.NotEqual(token1, (Token)token2);
        Assert.NotEqual(token1.GetHashCode(), token2.GetHashCode());

        Assert.False(token1.Equals(null));
    }

    [Fact]
    public void NonSql_Equality_BlockComment()
    {
        var token1 = new NonSql(NonSqlType.BlockComment, "comment") { Location = new Lexing.Location(1, 2, 3) };
        var token2 = new NonSql(NonSqlType.BlockComment, "comment") { Location = new Lexing.Location(4, 5, 6) };

        Assert.Equal(token1, token2);
        Assert.Equal(token1, (object)token2);
        Assert.Equal(token1, (Token)token2);
        Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
    }

    [Fact]
    public void NonSql_Inequality_BlockComment()
    {
        var token1 = new NonSql(NonSqlType.BlockComment, "comment") { Location = new Lexing.Location(1, 2, 3) };
        var token2 = new NonSql(NonSqlType.BlockComment, "different") { Location = new Lexing.Location(4, 5, 6) };

        Assert.NotEqual(token1, token2);
        Assert.NotEqual(token1, (object)token2);
        Assert.NotEqual(token1, (Token)token2);
        Assert.NotEqual(token1.GetHashCode(), token2.GetHashCode());

        Assert.False(token1.Equals(null));
    }

    [Fact]
    public void NonSql_Inequality_InlineComment_BlockComment()
    {
        var token1 = new NonSql(NonSqlType.InlineComment, "comment") { Prefix = "--", Location = new Lexing.Location(1, 2, 3) };
        var token2 = new NonSql(NonSqlType.BlockComment, "comment") { Location = new Lexing.Location(4, 5, 6) };

        Assert.NotEqual(token1, token2);
        Assert.NotEqual(token1, (object)token2);
        Assert.NotEqual(token1, (Token)token2);
        Assert.NotEqual(token1.GetHashCode(), token2.GetHashCode());

        Assert.False(token1.Equals(null));
    }

    public class TokenTestData : IEnumerable<TheoryDataRow<Token, Token>>
    {
        public IEnumerator<TheoryDataRow<Token, Token>> GetEnumerator()
        {
            foreach (var data in GetTestData())
            {
                yield return data;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static IEnumerable<TheoryDataRow<Token, Token>> GetTestData()
        {
            yield return new TheoryDataRow<Token, Token>(
                new MyTerminator("//") { Location = new Lexing.Location() },
                new MyTerminator("//") { Location = new Lexing.Location() });

            yield return new TheoryDataRow<Token, Token>(
                new NumericLiteral("123") { Location = new Lexing.Location() },
                new NumericLiteral("123") { Location = new Lexing.Location() });

            yield return new TheoryDataRow<Token, Token>(
                new NumericLiteral("123", true) { Location = new Lexing.Location() },
                new NumericLiteral("123", true) { Location = new Lexing.Location() });
        }
    }

    public class MyTerminatorSerializer : IXunitSerializer
    {
        public string Serialize(object value)
        {
            if (value is MyTerminator myTerminator)
            {
                return myTerminator.Delimiter;
            }
            throw new ArgumentException("Value is not a MyTerminator", nameof(value));
        }

        public object Deserialize(Type type, string value)
        {
            if (type == typeof(MyTerminator))
            {
                return new MyTerminator(value) { Location = new Lexing.Location() };
            }
            throw new ArgumentException("Type is not a MyTerminator", nameof(type));
        }

        public bool IsSerializable(Type type, object? value, out string failureReason)
        {
            if (value is MyTerminator)
            {
                failureReason = string.Empty;
                return true;
            }
            failureReason = "Value is not a MyTerminator";
            return false;
        }
    }

    public class NumericLiteralSerializer : IXunitSerializer
    {
        public string Serialize(object value)
        {
            if (value is NumericLiteral numericLiteral)
            {
                if (numericLiteral.IsLong)
                {
                    return numericLiteral.Value + "L";
                }
                return numericLiteral.Value;
            }
            throw new ArgumentException("Value is not a NumericLiteral", nameof(value));
        }

        public object Deserialize(Type type, string value)
        {
            if (type == typeof(NumericLiteral))
            {
                bool isLong = false;
                if (value.EndsWith('L'))
                {
                    isLong = true;
                    value = value[..^1];
                }
                return new NumericLiteral(value, isLong) { Location = new Lexing.Location() };
            }
            throw new ArgumentException("Type is not a NumericLiteral", nameof(type));
        }

        public bool IsSerializable(Type type, object? value, out string failureReason)
        {
            if (value is NumericLiteral)
            {
                failureReason = string.Empty;
                return true;
            }
            failureReason = "Value is not a NumericLiteral";
            return false;
        }
    }
}
