using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class TextParser(ILexer lexer, IParser parser) : IParseText
{
    private readonly IParser _parser = parser;
    private readonly ILexer _lexer = lexer;

    public SqlValueList<Statement> ParseText(string text, string? filename = null, int sourceId = 0)
    {
        List<Token> tokens = _lexer.Tokenize(text);
        return _parser.Parse([.. tokens], sourceId);
    }
}
