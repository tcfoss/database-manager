using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class ControlFlowParser
{
    private readonly Parser _p;
    private readonly Func<ParserState, Identifier> _parseIdentifier;

    public ControlFlowParser(Parser parser)
    {
        _p = parser;
        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
    }

    public BeginEnd ParseBeginEnd(ParserState state)
    {
        state.ExpectKeyword(Keyword.BEGIN);

        var ender = new EndSubstatements()
        {
            GetFinished = innerState => innerState.ParseKeyword(Keyword.END),
            GetEofException = innerState => innerState.ExpectedException("END")
        };

        SqlValueList<Statement> statements = _p.Parse(state, ender);

        return new BeginEnd(statements);
    }

    public virtual Statement ParseIf(ParserState state)
    {
        throw new NotSupportedException("IF statements require a dialect-specific parser.");
    }

    public DeclareConditionHandler ParseDeclareConditionHandler(ParserState state)
    {
        state.ExpectKeyword(Keyword.DECLARE);

        Keyword? next = (state.Next() as Word)?.Keyword;

        HandlerAction handlerAction = next switch
        {
            Keyword.CONTINUE => HandlerAction.Continue,
            Keyword.EXIT => HandlerAction.Exit,
            Keyword.UNDO => HandlerAction.Undo,
            _ => throw state.ExpectedException("CONTINUE", "EXIT", "UNDO")
        };

        state.ExpectKeywordsAll(Keyword.HANDLER, Keyword.FOR);

        SqlValueList<ConditionValue> conditions = state.ParseCommaSeparated(ParseConditionValuePartial);
        Statement statement = _p.ParseStatement(state);
        // if (state.PeekIs<Semicolon>())
        // {
        //     var sc = state.Next();
        //     if (sc.PreNonSql != null)
        //     {
        //         statement.Meta.AddPostNonSql(sc.PreNonSql.ToStatementNonSql());
        //     }
        // }
        return new DeclareConditionHandler(handlerAction, conditions, statement);
    }

    public DeclareCondition ParseDeclareCondition(ParserState state)
    {
        state.ExpectKeyword(Keyword.DECLARE);
        Identifier name = _parseIdentifier(state);
        state.ExpectKeywordsAll(Keyword.CONDITION, Keyword.FOR);
        ConditionValue condition = ParseConditionValueForDeclarePartial(state);
        return new DeclareCondition(name, condition);
    }

    public virtual DeclareLocalVariable ParseDeclareLocalVariable(ParserState state)
    {
        throw new NotSupportedException("Local variable declaration requires a dialect-specific parser.");
    }

    public DeclareCursor ParseDeclareCursor(ParserState state)
    {
        state.ExpectKeyword(Keyword.DECLARE);
        Identifier name = _parseIdentifier(state);
        state.ExpectKeywordsAll(Keyword.CURSOR, Keyword.FOR);
        Select query = _p.SelectParser.ParseSelect(state, null);
        return new DeclareCursor(name, query);
    }

    private static ConditionValue ParseConditionValueForDeclarePartial(ParserState state)
    {
        if (state.ParseKeyword(Keyword.SQLSTATE))
        {
            bool valueKeyword = state.ParseKeyword(Keyword.VALUE);
            string sqlState = ValueParser.ParseLiteralString(state);
            return new ConditionValue.SqlState(sqlState)
            {
                IncludeValueKeyword = valueKeyword
            };
        }
        if (state.Peek() is NumericLiteral)
        {
            uint value = ValueParser.ParseLiteralUInt(state);
            return new ConditionValue.ErrorCode(value);
        }
        throw state.ExpectedException("sql_error_code".Italic(), "SQLSTATE [VALUE] " + "sqlstate_value".Italic());
    }

    private ConditionValue ParseConditionValuePartial(ParserState state)
    {
        if (state.ParseKeywordSequence(Keyword.SQLSTATE, KeywordSearchCondition.Optional(Keyword.VALUE)).Count > 0)
        {
            return new ConditionValue.SqlState(ValueParser.ParseLiteralString(state));
        }
        if (state.ParseKeyword(Keyword.SQLWARNING))
        {
            return new ConditionValue.SqlWarning();
        }
        if (state.ParseKeyword(Keyword.SQLEXCEPTION))
        {
            return new ConditionValue.SqlException();
        }
        if (state.ParseKeywordsAll(Keyword.NOT, Keyword.FOUND))
        {
            return new ConditionValue.NotFound();
        }
        if (state.Peek() is NumericLiteral)
        {
            uint value = ValueParser.ParseLiteralUInt(state);
            return new ConditionValue.ErrorCode(value);
        }

        return new ConditionValue.ConditionName(_parseIdentifier(state));
    }

    public SetVariable ParseSetVariable(ParserState state)
    {
        state.ExpectKeyword(Keyword.SET);
        SqlValueList<Assignment> assignments = state.ParseCommaSeparated(_p.ComponentParser.ParseAssignment);

        return new SetVariable(assignments);
    }
}
