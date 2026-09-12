using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class EndSubstatements
{
    public required Func<ParserState, bool> GetFinished { get; init; }
    public required Func<ParserState, ParseException> GetEofException { get; init; }
}
