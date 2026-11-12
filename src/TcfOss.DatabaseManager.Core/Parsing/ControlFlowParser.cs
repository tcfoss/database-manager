using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Extensions;
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

    public BeginEnd ParsePgBeginEnd(ParserState state)
    {
        using ParserState.Scope scope = state.EnterContext(ParseContext.PgProceduralBlock);
        SqlValueList<Statement>? declarations = null;
        bool hasDeclare = state.ParseKeyword(Keyword.DECLARE);
        if (hasDeclare)
        {
            declarations = [];
            while (!state.PeekKeyword(Keyword.BEGIN))
            {
                Token start = state.Peek();
                if (start is Eof)
                {
                    throw state.ExpectedException("BEGIN");
                }
                PgBlockVariable declaration = ParsePgBlockVariable(state);
                Parser.AttachStatementMetadata(declaration, start, state);
                declarations.Add(declaration);
                state.ExpectToken<Semicolon>();
            }
            if (state.Peek().PreNonSql is { } beginTrivia && beginTrivia.SafeAny())
            {
                declarations.Add(beginTrivia.ToInertOnly());
            }
        }

        state.ExpectKeyword(Keyword.BEGIN);
        bool strictTerminator = hasDeclare || !_p.Grammar.Any(Grammar.My | Grammar.Ms) || state.StatementLabel?.Dialect == StatementLabelDialect.Pg;
        var blockEnd = new EndSubstatements
        {
            RequireTerminatorBeforeEnd = strictTerminator,
            GetFinished = s => s.PeekKeyword(Keyword.EXCEPTION) || s.PeekKeyword(Keyword.END),
            GetEofException = s => s.ExpectedException("EXCEPTION", "END")
        };

        SqlValueList<Statement> statements = _p.Parse(state, blockEnd);
        if (state.Peek().PreNonSql is { } blockEndTrivia && blockEndTrivia.SafeAny())
        {
            statements.Add(blockEndTrivia.ToInertOnly());
        }

        SqlValueList<PgExceptionHandler>? handlers = null;
        if (state.ParseKeyword(Keyword.EXCEPTION))
        {
            handlers = [];
            do
            {
                Token when = state.Peek();
                state.ExpectKeyword(Keyword.WHEN);
                SqlValueList<PgExceptionCondition> conditions = [ParsePgExceptionCondition(state)];
                while (state.ParseKeyword(Keyword.OR))
                {
                    conditions.Add(ParsePgExceptionCondition(state));
                }
                state.ExpectKeyword(Keyword.THEN);

                var handlerEnd = new EndSubstatements
                {
                    RequireTerminatorBeforeEnd = true,
                    GetFinished = s => s.PeekKeyword(Keyword.WHEN) || s.PeekKeyword(Keyword.END),
                    GetEofException = s => s.ExpectedException("WHEN", "END")
                };
                SqlValueList<Statement> handlerStatements = _p.Parse(state, handlerEnd);

                // Consider pulling this logic into the core Parser or into a helper function
                if (state.PeekKeyword(Keyword.END) && state.Peek().PreNonSql is { } handlerEndTrivia && handlerEndTrivia.SafeAny())
                {
                    handlerStatements.Add(handlerEndTrivia.ToInertOnly());
                }

                var handler = new PgExceptionHandler(conditions, handlerStatements);
                if (when.PreNonSql is { } whenTrivia && whenTrivia.SafeAny())
                {
                    handler.Meta.PreNonSql = whenTrivia.Items;
                }

                handlers.Add(handler);
            } while (state.PeekKeyword(Keyword.WHEN));
        }

        state.ExpectKeyword(Keyword.END);
        Identifier? endLabel = null;

        if (state.Peek() is Word && state.StatementLabel?.Dialect == StatementLabelDialect.Pg)
        {
            Token token = state.Peek();
            endLabel = _parseIdentifier(state);
            Identifier startLabel = state.StatementLabel.Value.Label;
            string startName = startLabel.QuoteStyle == QuoteStyle.None ? startLabel.Name.ToLowerInvariant() : startLabel.Name;
            string endName = endLabel.QuoteStyle == QuoteStyle.None ? endLabel.Name.ToLowerInvariant() : endLabel.Name;
            if (!string.Equals(startName, endName, StringComparison.Ordinal))
            {
                throw ParserState.ExpectedException("matching block label", token);
            }
        }

        PgBlockOptions? options = declarations != null || handlers != null || endLabel != null
            ? new PgBlockOptions(declarations, handlers, endLabel)
            : null;

        return new BeginEnd(statements) { PgOptions = options };
    }

    private PgBlockVariable ParsePgBlockVariable(ParserState state)
    {
        Identifier name = _parseIdentifier(state);
        bool constant = state.TryGetPseudoKeyword(out string text, out _, false)
            && string.Equals(text, "CONSTANT", StringComparison.OrdinalIgnoreCase);
        if (constant)
        {
            state.Next();
        }
        DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReference(state);

        ObjectName? collation = state.ParseKeyword(Keyword.COLLATE) ? _p.ComponentParser.ParseObjectName(state, "collation_name") : null;

        bool notNull = state.ParseKeywordsAll(Keyword.NOT, Keyword.NULL);

        PgBlockVariableDefault? def = null;

        PgInitializerOperator? marker = state.ParseKeyword(Keyword.DEFAULT)
            ? PgInitializerOperator.Default
            : state.ConsumeTokenIs<Walrus>() ? PgInitializerOperator.Assignment
            : state.ConsumeTokenIs<Equal>() ? PgInitializerOperator.Equal
            : null;

        if (marker != null)
        {
            def = new PgBlockVariableDefault(marker, _p.ExpressionParser.ParseExpr(state));
        }
        return new PgBlockVariable(name, dataType)
        {
            Constant = constant,
            Collation = collation,
            NotNull = notNull,
            Default = def
        };
    }

    private PgExceptionCondition ParsePgExceptionCondition(ParserState state)
    {
        if (state.ParseKeyword(Keyword.SQLSTATE))
        {
            return new PgExceptionCondition.SqlState(ValueParser.ParseLiteralString(state));
        }
        if (state.ParseKeyword(Keyword.OTHERS))
        {
            return new PgExceptionCondition.Others();
        }
        return new PgExceptionCondition.Named(_parseIdentifier(state));
    }

    public virtual Statement ParseIf(ParserState state)
    {
        throw new ParseException.DialectRequired("IF syntax", state.Peek().Location);
    }

    public virtual BeginAtomic ParseBeginAtomic(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.BEGIN, Keyword.ATOMIC);
        Token closing = state.Peek();
        var ender = new EndSubstatements
        {
            RequireTerminatorBeforeEnd = true,
            GetFinished = innerState =>
            {
                if (!innerState.PeekKeyword(Keyword.END))
                {
                    return false;
                }
                closing = innerState.Next();
                return true;
            },
            GetEofException = innerState => innerState.ExpectedException("END")
        };
        SqlValueList<Statement> statements = _p.Parse(state, ender);
        if (closing.PreNonSql.SafeAny())
        {
            statements.Add(closing.PreNonSql.ToInertOnly());
        }
        return new BeginAtomic(statements);
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
        throw new ParseException.DialectRequired("Local variable declaration", state.Peek().Location);
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
