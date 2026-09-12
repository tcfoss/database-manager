using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsControlFlowParser : ControlFlowParser
{
    private readonly MsParser _p;
    private readonly Func<ParserState, Expression> _parseExpr;
    private readonly Func<ParserState, Identifier> _parseIdentifier;

    public MsControlFlowParser(MsParser parser) : base(parser)
    {
        _p = parser;
        _parseExpr = _p.ExpressionParser.ParseExpr;
        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
    }

    /// <summary>
    /// Parses a T-SQL <c>IF</c> statement.
    /// Syntax: <c>IF &lt;bool-expr&gt; &lt;stmt&gt; [ELSE &lt;stmt&gt;]</c>.
    /// No <c>THEN</c>, no <c>ELSEIF</c>, no <c>END IF</c>; each branch is a
    /// single un-terminated statement.
    /// </summary>
    public override Statement ParseIf(ParserState state)
    {
        state.ExpectKeyword(Keyword.IF);
        Expression condition = _parseExpr(state);
        Statement thenStmt = _p.ParseStatement(state);

        Statement? elseStmt = null;
        if (state.ParseKeyword(Keyword.ELSE))
        {
            elseStmt = _p.ParseStatement(state);
        }

        return new If.Ms(condition, thenStmt)
        {
            ElseStatement = elseStmt,
        };
    }

    /// <summary>
    /// Parses a T-SQL <c>DECLARE</c> statement. T-SQL syntax differs from
    /// MySQL: each declaration is a <c>@name TYPE [= expr]</c> tuple and
    /// multiple tuples are comma-separated, e.g.
    /// <c>DECLARE @a INT = 0, @b VARCHAR(10) = 'x'</c>.
    /// </summary>
    public override DeclareLocalVariable ParseDeclareLocalVariable(ParserState state)
    {
        state.ExpectKeyword(Keyword.DECLARE);
        SqlValueList<MsVariableDeclaration> declarations =
            state.ParseCommaSeparated(ParseDeclareTuple);
        return new DeclareLocalVariable.Ms(declarations);
    }

    private MsVariableDeclaration ParseDeclareTuple(ParserState state)
    {
        Identifier name = _parseIdentifier(state);
        if (name.Sigil != SigilKind.Variable)
        {
            throw state.ExpectedException("local variable (@name)");
        }
        DataType dataType = _p.DataTypeParser.ParseDataType(state);
        Expression? initialValue = null;
        if (state.ConsumeTokenIs<Equal>())
        {
            initialValue = _parseExpr(state);
        }
        return new MsVariableDeclaration(name, dataType)
        {
            InitialValue = initialValue,
        };
    }
}
