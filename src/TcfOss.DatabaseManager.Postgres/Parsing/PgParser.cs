using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgParser : Parser
{
    public PgParser()
    {
        ComponentParser = new PgComponentParser(this);
        DataTypeParser = new PgDataTypeParser(this);
        ExpressionParser = new ExpressionParser(this);
        SelectParser = new SelectParser(this);
        DmlParser = new DmlParser(this);
        DdlParser = new DdlParser(this);
        TableParser = new TableParser(this);
        ControlFlowParser = new ControlFlowParser(this);
    }

    protected override Statement ParseCreate(ParserState state)
    {
        var (createOrLabel, viewAlgorithm, definer, securityContext, aggregate, temporary) = ParseCreatePrefix(state);
        return state.Peek() switch
        {
            Word { Keyword: Keyword.TYPE } when createOrLabel == null && viewAlgorithm == null && definer == null && securityContext == null && !aggregate && !temporary => DdlParser.ParseCreateType(state),
            Word { Keyword: Keyword.TABLE } => TableParser.ParseCreate(state, createOrLabel == CreateOrLabel.OrReplace, temporary),
            Word { Keyword: Keyword.TRIGGER } => DdlParser.ParseCreateTrigger(state, createOrLabel, definer),
            Word { Keyword: Keyword.FUNCTION } => DdlParser.ParseCreateFunction(state, aggregate, createOrLabel, definer),
            Word { Keyword: Keyword.PROCEDURE or Keyword.PROC } => DdlParser.ParseCreateProcedure(state, createOrLabel, definer),
            Word { Keyword: Keyword.VIEW } => DdlParser.ParseCreateView(state, createOrLabel, definer, viewAlgorithm, securityContext),
            Word { Keyword: Keyword.INDEX or Keyword.UNIQUE or Keyword.FULLTEXT or Keyword.SPATIAL or Keyword.CLUSTERED or Keyword.NONCLUSTERED } => DdlParser.ParseCreateIndex(state, createOrLabel),
            _ => throw state.ExpectedException("TABLE", "TRIGGER", "VIEW", "FUNCTION", "PROCEDURE", "INDEX", "TYPE"),
        };
    }
}
