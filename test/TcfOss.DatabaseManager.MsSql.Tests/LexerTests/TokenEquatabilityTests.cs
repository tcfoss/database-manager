using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.MsSql.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MsSql.Tests.LexerTests;

public class TokenEquatabilityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BatchSeparator_Equality(int count)
    {
        var token1 = new MsBatchSeparator(count) { Location = new() };
        var token2 = new MsBatchSeparator(count) { Location = new() };

        Assert.Equal(token1, token2);
        Assert.Equal(token1, (object)token2);
        Assert.Equal(token1, (Token)token2);
        Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
    }

    [Fact]
    public void BatchSeparator_Equality_CountDefaultsTo1()
    {
        var token1 = new MsBatchSeparator() { Location = new() };
        var token2 = new MsBatchSeparator(1) { Location = new() };

        Assert.Equal(token1, token2);
        Assert.Equal(token1, (object)token2);
        Assert.Equal(token1, (Token)token2);
        Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
    }

    [Fact]
    public void BatchSeparator_Inequality()
    {
        var token1 = new MsBatchSeparator(1) { Location = new() };
        var token2 = new MsBatchSeparator(2) { Location = new() };

        Assert.NotEqual(token1, token2);
        Assert.NotEqual(token1, (object)token2);
        Assert.NotEqual(token1, (Token)token2);
        Assert.NotEqual(token1.GetHashCode(), token2.GetHashCode());

        Assert.False(token1.Equals(null));
    }
}
