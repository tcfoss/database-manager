using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyControlFlowParser : ControlFlowParser
{
    private readonly MyParser _p;
    private readonly Func<ParserState, Expression> _parseExpr;
    private readonly Func<ParserState, Identifier> _parseIdentifier;

    public MyControlFlowParser(MyParser parser) : base(parser)
    {
        _p = parser;
        _parseExpr = _p.ExpressionParser.ParseExpr;
        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
    }

    public override Statement ParseIf(ParserState state)
    {
        return ParseMyOrPgIf(state, false);
    }

    public override DeclareLocalVariable ParseDeclareLocalVariable(ParserState state)
    {
        state.ExpectKeyword(Keyword.DECLARE);
        SqlValueList<Identifier> names = state.ParseCommaSeparated(_parseIdentifier);
        DataType dataType = _p.DataTypeParser.ParseDataType(state);
        Expression? defaultValue = null;
        if (state.ParseKeyword(Keyword.DEFAULT))
        {
            defaultValue = _parseExpr(state);
        }
        return new DeclareLocalVariable.My(names, dataType)
        {
            DefaultValue = defaultValue
        };
    }
}
