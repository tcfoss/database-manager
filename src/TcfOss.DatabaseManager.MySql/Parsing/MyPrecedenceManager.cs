using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyPrecedenceManager : PrecedenceManager
{
    protected override bool SupportsPostgresPredicateSyntax => false;

    public override short GetPrecedence(Precedence precedence)
    {
        return precedence switch
        {
            Precedence.UnarySign => 40,
            Precedence.Comparison => 19,
            Precedence.Like => 18,
            _ => base.GetPrecedence(precedence)
        };
    }

    protected override Precedence? GetOperatorPrecedence(ParserState state)
    {
        return state.Peek() is DoublePipe ? Precedence.LogicalOr : base.GetOperatorPrecedence(state);
    }
}
