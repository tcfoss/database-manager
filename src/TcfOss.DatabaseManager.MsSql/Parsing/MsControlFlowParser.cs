using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
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

    public override Statement ParseIf(ParserState state)
    {
        return ParseMsIf(state);
    }

    public override While ParseWhile(ParserState state)
    {
        return ParseMsWhile(state);
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

    public override TransactionNoun ParseRequiredTransactionNoun(ParserState state)
    {
        if (state.ParseKeyword(Keyword.TRANSACTION))
        {
            return TransactionNoun.Transaction;
        }
        if (state.ParseKeyword(Keyword.TRAN))
        {
            return TransactionNoun.Tran;
        }
        throw state.ExpectedException("TRAN", "TRANSACTION");
    }

    public override TransactionNoun? ParseOptionalTransactionNoun(ParserState state)
    {
        if (state.ParseKeyword(Keyword.TRANSACTION))
        {
            return TransactionNoun.Transaction;
        }
        if (state.ParseKeyword(Keyword.TRAN))
        {
            return TransactionNoun.Tran;
        }
        return null;
    }

    protected override TransactionOptions ParseTransactionOptions(ParserState state)
    {
        return new TransactionOptions(Chain: null, Release: null);
    }

    protected override CursorDirection? ParseCursorDirection(ParserState state, bool allowMany)
    {
        CursorDirection? basic = state.Peek() switch
        {
            Word { Keyword: Keyword.NEXT } => new CursorDirection.Next(),
            Word { Keyword: Keyword.PRIOR } => new CursorDirection.Prior(),
            Word { Keyword: Keyword.FIRST } => new CursorDirection.First(),
            Word { Keyword: Keyword.LAST } => new CursorDirection.Last(),
            _ => null
        };
        if (basic is not null)
        {
            state.Next();
            return basic;
        }

        Keyword direction = state.ParseKeywordsAny(Keyword.ABSOLUTE, Keyword.RELATIVE);
        if (direction != Keyword.undefined)
        {
            Expression count;
            if (state.Peek() is Word { Sigil: SigilKind.Variable })
            {
                count = new SingleIdentifier(_parseIdentifier(state));
            }
            else
            {
                count = new LiteralValue(ValueParser.ParseNumericValue(state));
            }
            return direction switch
            {
                Keyword.ABSOLUTE => new CursorDirection.Absolute(count),
                Keyword.RELATIVE => new CursorDirection.Relative(count),
                _ => throw new DefensiveGuardException("absolute/relative cursor direction", direction.ToString())
            };
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
