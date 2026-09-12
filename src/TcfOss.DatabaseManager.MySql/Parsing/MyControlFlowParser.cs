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
        var ifEnder = new EndSubstatements()
        {
            GetFinished = s =>
            {
                if (s.PeekKeywordsAllEqual(Keyword.END, Keyword.IF))
                {
                    return true;
                }
                if (s.PeekKeyword(Keyword.ELSEIF))
                {
                    return true;
                }
                if (s.PeekKeyword(Keyword.ELSE))
                {
                    return true;
                }
                return false;
            },
            GetEofException = s => s.ExpectedException("ELSEIF", "ELSE", "END IF")
        };

        state.ExpectKeyword(Keyword.IF);

        Expression ifExpression = _parseExpr(state);

        state.ExpectKeyword(Keyword.THEN);

        SqlValueList<Statement> ifStatements = _p.Parse(state, ifEnder);

        if (state.ParseKeywordsAll(Keyword.END, Keyword.IF))
        {
            return new If.My(ifExpression, ifStatements);
        }

        SqlValueList<If.ElseIf> elseIfBlocks = [];

        while (true)
        {
            if (!state.ParseKeyword(Keyword.ELSEIF))
            {
                break;
            }
            Expression elseIfCondition = _parseExpr(state);

            state.ExpectKeyword(Keyword.THEN);

            SqlValueList<Statement> elseIfStatements = _p.Parse(state, ifEnder);

            elseIfBlocks.Add(new If.ElseIf(elseIfCondition, elseIfStatements));
        }

        if (state.ParseKeywordsAll(Keyword.END, Keyword.IF))
        {
            return new If.My(ifExpression, ifStatements)
            {
                ElseIfs = elseIfBlocks
            };
        }

        SqlValueList<Statement>? elseStatements = null;

        if (state.ParseKeyword(Keyword.ELSE))
        {
            elseStatements = _p.Parse(state, new EndSubstatements()
            {
                GetFinished = s => s.ParseKeywordsAll(Keyword.END, Keyword.IF),
                GetEofException = s => s.ExpectedException("END IF")
            });
        }

        return new If.My(ifExpression, ifStatements)
        {
            ElseIfs = elseIfBlocks,
            ElseStatements = elseStatements
        };
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
