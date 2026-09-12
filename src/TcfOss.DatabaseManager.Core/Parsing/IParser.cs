using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Parsing;

// Many attributes are accessed only via more specific type (for now), but we'll keep them on
// the interface for the sake of code using this as a library.
// ReSharper disable UnusedMemberInSuper.Global
public interface IParser
{
    DataTypeParser DataTypeParser { get; }
    ExpressionParser ExpressionParser { get; }
    ValueParser ValueParser { get; }
    ComponentParser ComponentParser { get; }
    TableParser TableParser { get; }
    SelectParser SelectParser { get; }
    DmlParser DmlParser { get; }
    DdlParser DdlParser { get; }
    ControlFlowParser ControlFlowParser { get; }

    SqlValueList<Statement> Parse(Token[] tokens, int sourceId = 0);
    SqlValueList<Statement> Parse(ParserState state, EndSubstatements? endChecker);
    Statement ParseStatement(ParserState state);
}
