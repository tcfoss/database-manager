using FluentAssertions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Tests.LexerTests;

public class LexerTestBase
{
    protected static void Compare(IList<Token> expected, IList<Token> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.IsType(expected[i].GetType(), actual[i]);
            expected[i].Should().BeEquivalentTo(actual[i], options =>
            {
                return options.RespectingRuntimeTypes().Excluding(t => t.Location).Excluding(t => t.PreNonSql);
            });
        }
    }
}
