using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class Parser<TRunState> : ParserBase
    where TRunState : struct
{
    public override SqlValueList<Statement> Parse(ParserState state, EndSubstatements? endChecker)
    {
        SqlValueList<Statement> statements = [];
        bool expectingTerminator = false;
        TRunState runState = CreateRunState();

        while (true)
        {
            expectingTerminator = ConsumeTerminators(state, ref runState, expectingTerminator);

            Token next = state.Peek();
            if (next is Eof)
            {
                if (endChecker != null)
                {
                    throw endChecker.GetEofException(state);
                }
                if (next.PreNonSql.SafeAny())
                {
                    statements.Add(next.PreNonSql.ToInertOnly());
                }
                break;
            }

            if (expectingTerminator && endChecker is { RequireTerminatorBeforeEnd: true })
            {
                throw ParserState.ExpectedException("end of statement", next);
            }

            if (endChecker?.GetFinished(state) ?? false)
            {
                if (next.PreNonSql.SafeAny() && endChecker.PreserveEndTrivia(next))
                {
                    statements.Add(next.PreNonSql.ToInertOnly());
                }
                break;
            }

            if (expectingTerminator)
            {
                throw ParserState.ExpectedException("end of statement", next);
            }

            expectingTerminator = ParseNextStatement(state, ref runState, statements);
        }

        return statements;
    }

    private static TRunState CreateRunState()
    {
        return new TRunState();
    }

    protected virtual bool ConsumeTerminators(ParserState state, ref TRunState runState, bool expectingTerminator)
    {
        while (state.ConsumeTokenIs<Semicolon>())
        {
            expectingTerminator = false;
        }
        return expectingTerminator;
    }

    protected virtual bool ParseNextStatement(ParserState state, ref TRunState runState, SqlValueList<Statement> statements)
    {
        statements.Add(ParseStatement(state));
        return true;
    }
}

public class Parser : Parser<ParserRunState>
{
}
