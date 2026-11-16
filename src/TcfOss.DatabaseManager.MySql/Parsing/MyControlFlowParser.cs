using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

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

    public override While ParseWhile(ParserState state)
    {
        return ParseMyOrPgWhile(state);
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

    public override Return ParseReturn(ParserState state)
    {
        state.ExpectKeyword(Keyword.RETURN);
        return new Return(_parseExpr(state));
    }

    public override TransactionNoun? ParseOptionalTransactionNoun(ParserState state)
    {
        if (state.ParseKeyword(Keyword.WORK))
        {
            return TransactionNoun.Work;
        }
        return null;
    }

    public override Identifier? ParseTransactionName(ParserState state, TransactionNoun? noun)
    {
        return null;
    }

    protected override TransactionMode? ParseOptionalTransactionMode(ParserState state)
    {
        return ParseOptionalMyTransactionMode(state);
    }

    public override Statement ParseFetch(ParserState state)
    {
        if (state.ParseKeywordsAll(Keyword.FETCH, Keyword.GROUP, Keyword.NEXT, Keyword.ROW))
        {
            return new FetchGroupNextRow();
        }
        return base.ParseFetch(state);
    }

    protected override CursorDirection? ParseCursorDirection(ParserState state, bool allowMany)
    {
        if (state.ParseKeyword(Keyword.NEXT))
        {
            return new CursorDirection.Next();
        }
        return null;
    }

    protected override CursorNameConnector? ParseCursorNameConnector(ParserState state, CursorDirection? direction)
    {
        if (direction is not null)
        {
            state.ExpectKeyword(Keyword.FROM);
            return CursorNameConnector.From;
        }
        else if (state.ParseKeyword(Keyword.FROM))
        {
            return CursorNameConnector.From;
        }
        return null;
    }
}
