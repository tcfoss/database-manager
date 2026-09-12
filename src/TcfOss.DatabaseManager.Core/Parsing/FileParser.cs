using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class FileParser(ILexer? lexer = null, IParser? parser = null)
{
    private readonly ILexer _lexer = lexer ?? new GenericLexer();
    private readonly IParser _parser = parser ?? new Parser();

    public SqlValueList<Statement> ParseFile(string filePath, out string text)
    {
        string fullPath = Path.GetFullPath(filePath);

        text = File.ReadAllText(fullPath);

        try
        {
            List<Token> tokens = _lexer.Tokenize(text);
            return _parser.Parse([.. tokens]);
        }
        catch (LexException ex)
        {
            throw new LexException.WithText(ex, fullPath, text);
        }
        catch (ParseException ex)
        {
            throw new ParseException.WithText(ex, filePath, text);
        }
    }
}
