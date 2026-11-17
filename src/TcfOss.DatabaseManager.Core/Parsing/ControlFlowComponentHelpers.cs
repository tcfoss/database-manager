using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Expressions.Attributes;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class ControlFlowComponentHelpers
{
    public static PgCursorScrollMode? ParsePgCursorScrollMode(ParserState state)
    {
        if (state.ParseKeyword(Keyword.NO))
        {
            state.ExpectKeyword(Keyword.SCROLL);
            return PgCursorScrollMode.NoScroll;
        }
        return state.ParseKeyword(Keyword.SCROLL) ? PgCursorScrollMode.Scroll : null;
    }

    public static PgCursorHoldMode? ParsePgCursorHoldMode(ParserState state)
    {
        if (state.ParseKeywordsAll(Keyword.WITH, Keyword.HOLD))
        {
            return PgCursorHoldMode.WithHold;
        }
        if (state.ParseKeywordsAll(Keyword.WITHOUT, Keyword.HOLD))
        {
            return PgCursorHoldMode.WithoutHold;
        }
        return null;
    }

    public static bool IsPgCursorArgument(FunctionArgument argument)
    {
        return argument switch
        {
            FunctionArgument.Unnamed
            {
                Argument: FunctionArgumentExpression.FunctionExpression
            } => true,
            FunctionArgument.Named
            {
                Argument: FunctionArgumentExpression.FunctionExpression,
                Operator: var op
            } => op.Kind is FunctionArgumentOperator.ValueKind.Assignment or FunctionArgumentOperator.ValueKind.FatArrow,
            _ => false
        };
    }

    public static bool HasPgDeclareBlockOpener(ParserState state)
    {
        bool afterTerminator = true;
        for (int i = 1; ; i++)
        {
            Token token = state.PeekNth(i);
            if (token is Eof)
            {
                return false;
            }
            if (afterTerminator && token is Word { Keyword: Keyword.BEGIN })
            {
                return true;
            }
            afterTerminator = token is Semicolon;
        }
    }

    public static bool MaybeDeclareCursor(ParserState state, int maxLookahead = 8)
    {
        if (!(state.Pg || state.My || state.Ms))
        {
            return false;
        }

        for (int i = 1; i < maxLookahead; i++)
        {
            Token token = state.PeekNth(i);
            if (token is Word { Keyword: Keyword.CURSOR })
            {
                return true;
            }
            if (token is Semicolon or Eof)
            {
                break;
            }
        }
        return false;
    }

    public static bool CanParsePgSqlTransaction(ParserState state)
    {
        return state.Pg
            && (state.Context & (ParseContext.PgProceduralBlock | ParseContext.BeginAtomicBody | ParseContext.ExpectBeginAtomic | ParseContext.RoutineStatementBody)) == 0;
    }
}
