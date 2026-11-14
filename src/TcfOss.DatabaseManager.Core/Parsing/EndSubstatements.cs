using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class EndSubstatements
{
    public bool RequireTerminatorBeforeEnd { get; init; }
    public Func<Token, bool> PreserveEndTrivia { get; init; } = _ => true;
    public required Func<ParserState, bool> GetFinished { get; init; }
    public required Func<ParserState, ParseException> GetEofException { get; init; }
}
