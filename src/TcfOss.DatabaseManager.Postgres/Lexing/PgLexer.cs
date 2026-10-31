using TcfOss.DatabaseManager.Core.Lexing;

namespace TcfOss.DatabaseManager.Postgres.Lexing;

public class PgLexer : Lexer<RunState>
{
    protected override bool SupportBackslashQuoteEscape => false;
    protected override bool SupportBracketQuotedIdentifiers => false;
}
