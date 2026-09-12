using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Attributes;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyExpressionParser(MyParser parser)
    : ExpressionParser(parser)
{
    private readonly MyParser _p = parser;

    protected override Expression ParsePrefix(ParserState state)
    {
        Token token = state.Peek();

        return token switch
        {
            Word { Keyword: Keyword.MATCH } => ParseMatchAgainst(state),
            _ => base.ParsePrefix(state)
        };
    }

    private MatchAgainst ParseMatchAgainst(ParserState state)
    {
        state.ExpectKeyword(Keyword.MATCH);

        SqlValueList<Expression> columns = state.ParseParenthesizedCommaSeparated(ParseMultipart, false);

        state.ExpectKeyword(Keyword.AGAINST);

        state.ExpectLeftParen();

        // Need something higher-precedence than IN, since IN is used for the modifier.
        Expression matchValue = ParseSubExpression(state, _p.PrecedenceManager.GetPrecedence(Precedence.Between));

        MatchAgainstModifier? modifier = null;

        if (state.ParseKeywordsAll(Keyword.IN, Keyword.NATURAL, Keyword.LANGUAGE, Keyword.MODE, Keyword.WITH, Keyword.QUERY, Keyword.EXPANSION))
        {
            modifier = MatchAgainstModifier.NaturalLanguageModeWithQueryExpansion;
        }
        else if (state.ParseKeywordsAll(Keyword.IN, Keyword.NATURAL, Keyword.LANGUAGE, Keyword.MODE))
        {
            modifier = MatchAgainstModifier.NaturalLanguageMode;
        }
        else if (state.ParseKeywordsAll(Keyword.IN, Keyword.BOOLEAN, Keyword.MODE))
        {
            modifier = MatchAgainstModifier.BooleanMode;
        }
        else if (state.ParseKeywordsAll(Keyword.WITH, Keyword.QUERY, Keyword.EXPANSION))
        {
            modifier = MatchAgainstModifier.QueryExpansion;
        }

        state.ExpectRightParen();

        return new MatchAgainst(columns, matchValue, modifier);
    }
}
