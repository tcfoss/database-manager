using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgComponentParser(Parser parser) : ComponentParser(parser)
{
    public override Identifier ParseIdentifier(ParserState state, string expectedLabel)
    {
        Token token = state.Next();
        if (token is Word { QuoteStyle: QuoteStyle.None or QuoteStyle.Ansi, Sigil: SigilKind.None } word
            && word.Value.Length > 0
            && (word.QuoteStyle == QuoteStyle.Ansi || char.IsLetter(word.Value[0]) || word.Value[0] == '_'))
        {
            return word.ToIdentifier(state.SourceId);
        }
        throw ParserState.ExpectedCategoryException(expectedLabel, token);
    }
}
