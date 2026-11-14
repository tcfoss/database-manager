using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;

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
}
