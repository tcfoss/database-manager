using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgComponentParser(Parser parser) : ComponentParser(parser)
{
    protected override Limit? ParseOptionalCommaLimit(ParserState state, Expression? offset)
    {
        return null;
    }

    public override Identifier ParseIdentifier(ParserState state, string expectedLabel)
    {
        Token token = state.Next();
        if (token is Word { QuoteStyle: QuoteStyle.None or QuoteStyle.Ansi, Sigil: SigilKind.None, Value.Length: > 0 } word
            && (word.QuoteStyle == QuoteStyle.Ansi || char.IsLetter(word.Value[0]) || word.Value[0] == '_'))
        {
            return word.ToIdentifier(state.SourceId);
        }
        throw ParserState.ExpectedCategoryException(expectedLabel, token);
    }

    public override CreateOrLabel? ParseCreateOrLabel(ParserState state)
    {
        if (state.ParseKeywordsAll(Keyword.OR, Keyword.REPLACE))
        {
            return CreateOrLabel.OrReplace;
        }
        return null;
    }
}
