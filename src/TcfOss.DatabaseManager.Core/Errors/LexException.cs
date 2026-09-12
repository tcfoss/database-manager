using System.Text;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Resources;

namespace TcfOss.DatabaseManager.Core.Errors;

public abstract class LexException(string? message, Location? location, Exception? innerException = null) : LocatedException(message, location, innerException)
{
    public class UnexpectedCharacter(char character, Location? location = null)
        : LexException(s_compositeFormat.Apply(character), location)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Lex_InvalidCharacter);
    }

    public class UnterminatedString(Location? location = null)
        : LexException(ErrorMessages.Err_Lex_UnterminatedStringLiteral, location);

    public class UnterminatedBlockComment(Location? location = null)
        : LexException(ErrorMessages.Err_Lex_UnterminatedBlockComment, location);

    public class UnterminatedQuotedIdentifier(char expectedClosingQuote, Location? location = null)
        : LexException(s_compositeFormat.Apply(expectedClosingQuote), location)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Lex_UnterminatedQuotedIdentifier);
    }

    public class ExpectedSymbolSequence(Location? location = null)
        : LexException(ErrorMessages.Err_Lex_ExpectedSymbolSequence, location);

    public class WithText(LexException originalException, string? filename, string? text) : LexException(GetMessage(originalException, filename, text), originalException.Location, originalException)
    {
        private static string GetMessage(LexException originalException, string? filename, string? text)
        {
            return s_errorWithType.Apply(ErrorMessages.Err_Lex, originalException.GetErrorMessage(filename, text));
        }
    }
}
