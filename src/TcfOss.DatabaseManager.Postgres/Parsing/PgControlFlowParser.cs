using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgControlFlowParser(Parser parser) : ControlFlowParser(parser)
{
    public override Statement ParseIf(ParserState state)
    {
        return ParseMyOrPgIf(state, true);
    }

    public override While ParseWhile(ParserState state)
    {
        return ParseMyOrPgWhile(state);
    }

    public override TransactionNoun? ParseOptionalTransactionNoun(ParserState state)
    {
        if (state.ParseKeyword(Keyword.TRANSACTION))
        {
            return TransactionNoun.Transaction;
        }
        if (state.ParseKeyword(Keyword.WORK))
        {
            return TransactionNoun.Work;
        }
        return null;
    }

    public override Identifier? ParseTransactionName(ParserState state, TransactionNoun? noun)
    {
        return null;
    }

    protected override TransactionMode? ParseOptionalTransactionMode(ParserState state)
    {
        return ParseOptionalPgTransactionMode(state);
    }

    protected override TransactionOptions ParseTransactionOptions(ParserState state)
    {
        TransactionChainOption? chain = null;
        if (state.ParseKeyword(Keyword.AND))
        {
            bool negated = state.ParseKeyword(Keyword.NO);
            state.ExpectKeyword(Keyword.CHAIN);
            chain = new TransactionChainOption { Negated = negated };
        }
        return new TransactionOptions(chain, Release: null);
    }
}
