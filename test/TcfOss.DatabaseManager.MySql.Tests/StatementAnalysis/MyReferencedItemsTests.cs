using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.StatementAnalysis;

public class MyReferencedItemsTests
{
    private static string N(ItemRef item) => string.Join(".", item.Identifiers.Select(i => i.Name));

    [Fact]
    public void MatchAgainstExpression()
    {
        var text = "MATCH(a, b, c) AGAINST(d IN NATURAL LANGUAGE MODE)";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(4, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));
        Assert.Equal("a", N(items[0]));
        Assert.Equal("b", N(items[1]));
        Assert.Equal("c", N(items[2]));
        Assert.Equal("d", N(items[3]));
    }

    private static Expression ParseExpression(string sql)
    {
        var tokens = new MyLexer().Tokenize(sql);
        var state = new ParserState([.. tokens]);
        return new MyParser().ExpressionParser.ParseExpr(state);
    }
}
