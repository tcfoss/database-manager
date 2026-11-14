using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using NonSql = TcfOss.DatabaseManager.Core.Statements.Components.NonSql;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class ControlFlowParser
{
    private readonly Parser _p;
    private readonly Func<ParserState, Identifier> _parseIdentifier;
    private readonly Func<ParserState, Expression> _parseExpr;

    public ControlFlowParser(Parser parser)
    {
        _p = parser;
        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
        _parseExpr = _p.ExpressionParser.ParseExpr;
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
        bool strictTerminator = hasDeclare || !(state.My || state.Ms) || state.StatementLabel?.Dialect == StatementLabelDialect.Pg;
        var blockEnd = new EndSubstatements
        {
            RequireTerminatorBeforeEnd = strictTerminator,
            GetFinished = s => s.PeekKeyword(Keyword.EXCEPTION) || s.PeekKeyword(Keyword.END),
            GetEofException = s => s.ExpectedException("EXCEPTION", "END")
        };

        SqlValueList<Statement> statements = _p.Parse(state, blockEnd);

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
                    PreserveEndTrivia = token => token is Word { Keyword: Keyword.END },
                    GetFinished = s => s.PeekKeyword(Keyword.WHEN) || s.PeekKeyword(Keyword.END),
                    GetEofException = s => s.ExpectedException("WHEN", "END")
                };
                SqlValueList<Statement> handlerStatements = _p.Parse(state, handlerEnd);

                var handler = new PgExceptionHandler(conditions, handlerStatements);
                if (when.PreNonSql is { } whenTrivia && whenTrivia.SafeAny())
                {
                    handler.Meta.PreNonSql = whenTrivia.Items;
                }

                handlers.Add(handler);
            } while (state.PeekKeyword(Keyword.WHEN));
        }

        state.ExpectKeyword(Keyword.END);
        Identifier? endLabel = ParsePgEndLabel(state, "matching block label");

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

    public Loop ParseLoop(ParserState state)
    {
        state.ExpectKeyword(Keyword.LOOP);
        SqlValueList<Statement> statements = ParseLoopStatements(state, Keyword.LOOP);
        Identifier? endLabel = ParseLoopEndLabel(state);
        return new Loop(statements)
        {
            EndLabel = endLabel
        };
    }

    public virtual While ParseWhile(ParserState state)
    {
        if ((state.My || state.PgPl) && state.TryParse(ParseMyOrPgWhile, out While? blocked))
        {
            return blocked;
        }
        if (state.Ms && state.TryParse(ParseMsWhile, out While.Ms? inline))
        {
            return inline;
        }
        throw new ParseException.DialectRequired("WHILE syntax", state.Peek().Location);
    }

    protected While ParseMyOrPgWhile(ParserState state)
    {
        state.ExpectKeyword(Keyword.WHILE);
        Expression condition = _parseExpr(state);
        if (state.PgPl && state.ParseKeyword(Keyword.LOOP))
        {
            SqlValueList<Statement> statements = ParseLoopStatements(state, Keyword.LOOP);
            return new While.Pg(condition, statements) { EndLabel = ParseLoopEndLabel(state) };
        }
        if (state.My && state.ParseKeyword(Keyword.DO))
        {
            SqlValueList<Statement> statements = ParseLoopStatements(state, Keyword.WHILE);
            return new While.My(condition, statements) { EndLabel = ParseLoopEndLabel(state) };
        }
        throw state.ExpectedException(state.My && state.PgPl ? ["DO", "LOOP"] : state.PgPl ? ["LOOP"] : ["DO"]);
    }

    protected While.Ms ParseMsWhile(ParserState state)
    {
        state.ExpectKeyword(Keyword.WHILE);
        Expression condition = _parseExpr(state);
        return new While.Ms(condition, _p.ParseStatement(state));
    }

    public PgFor ParsePgFor(ParserState state)
    {
        state.ExpectKeyword(Keyword.FOR);
        Token targetStart = state.Peek();
        SqlValueList<ObjectName> targets = state.ParseCommaSeparated(_p.ComponentParser.ParseObjectName);
        state.ExpectKeyword(Keyword.IN);

        PgForSource source = ParsePgForSource(state);
        if (source is PgForSource.Range && (targets.Count != 1 || targets[0].Values.Count != 1))
        {
            throw ParserState.ExpectedCategoryException("single integer loop variable", targetStart);
        }

        Token loop = state.Peek();
        state.ExpectKeyword(Keyword.LOOP);
        SqlValueList<Statement> statements = ParseLoopStatements(state, Keyword.LOOP);
        return new PgFor(targets, source, statements)
        {
            EndLabel = ParseLoopEndLabel(state),
            LoopMeta = new() { PreNonSql = loop.PreNonSql?.Items }
        };
    }

    private static ParserState ReadPgForQuery(ParserState state)
    {
        List<Token> tokens = [];
        int depth = 0;
        while (true)
        {
            Token token = state.Peek();
            if (depth == 0 && token is Word { Keyword: Keyword.LOOP }
                && tokens.LastOrDefault() is not Dot && state.PeekNth(1) is not Dot)
            {
                // Keep LOOP out of the SQL parser, where it could be consumed as an implicit alias.
                tokens.Add(new Eof { Location = token.Location });
                return new ParserState(state.Grammar, [.. tokens], state.SourceId);
            }
            if (token is Eof or Semicolon)
            {
                throw state.ExpectedException("LOOP");
            }
            if (token is ParenOpen or BracketOpen)
            {
                depth++;
            }
            else if (token is ParenClose or BracketClose)
            {
                depth--;
            }
            tokens.Add(state.Next());
        }
    }

    private PgForSource ParsePgForSource(ParserState state)
    {
        if (state.ParseKeyword(Keyword.EXECUTE))
        {
            Expression command = _parseExpr(state);
            SqlValueList<Expression>? parameters = state.ParseKeyword(Keyword.USING)
                ? state.ParseCommaSeparated(_parseExpr)
                : null;
            return new PgForSource.Execute(command) { Using = parameters };
        }

        bool reverse = state.ParseKeyword(Keyword.REVERSE);

        Expression? lower;
        if (reverse)
        {
            lower = _parseExpr(state);
            state.ExpectToken<DotDot>();
        }
        else
        {
            _ = state.TryParse(s =>
            {
                Expression expression = _parseExpr(s);
                return s.ConsumeTokenIs<DotDot>() ? expression : null;
            }, out lower);
        }
        if (lower != null)
        {
            Expression upper = _parseExpr(state);
            Expression? step = state.ParseKeyword(Keyword.BY) ? _parseExpr(state) : null;
            return new PgForSource.Range(lower, upper) { Reverse = reverse, Step = step };
        }

        ParserState queryState = ReadPgForQuery(state);
        Token queryStart = queryState.Peek();
        Statement query = _p.ParseStatement(queryState);
        if (queryState.Peek() is not Eof)
        {
            throw queryState.ExpectedException("LOOP");
        }
        bool returnsRows = query switch
        {
            Select select => select.SelectInto == null && IsPgForSelectBody(select.Body),
            Insert { Returning: not null } or Update { Returning: not null } or Delete { Returning: not null } => true,
            _ => false
        };
        if (!returnsRows)
        {
            throw ParserState.ExpectedCategoryException("row-returning query", queryStart);
        }
        return new PgForSource.Query(query);
    }

    private static bool IsPgForSelectBody(SelectBody body)
    {
        return body switch
        {
            SelectBody.SimpleSelectQuery simple => simple.Query.SelectInto == null,
            SelectBody.Nested nested => nested.Query.SelectInto == null && IsPgForSelectBody(nested.Query.Body),
            SelectBody.SetOperation operation => IsPgForSelectBody(operation.Left) && IsPgForSelectBody(operation.Right),
            SelectBody.ValuesQuery => true,
            _ => false
        };
    }

    private SqlValueList<Statement> ParseLoopStatements(ParserState state, Keyword end)
    {
        string expected = $"END {end}";
        var ender = new EndSubstatements
        {
            RequireTerminatorBeforeEnd = state.PgPl || end == Keyword.WHILE,
            GetFinished = s => s.PeekKeywordsAll(Keyword.END, end),
            GetEofException = s => s.ExpectedException(expected)
        };
        SqlValueList<Statement> statements = _p.Parse(state, ender);
        if (end == Keyword.WHILE && !statements.NonInert().Any())
        {
            throw state.ExpectedException("statement");
        }
        state.ExpectKeywordsAll(Keyword.END, end);
        return statements;
    }

    private Identifier? ParseLoopEndLabel(ParserState state)
    {
        if (state.StatementLabel?.Dialect == StatementLabelDialect.Pg)
        {
            return ParsePgEndLabel(state, "matching loop label");
        }
        Identifier? label = state.StatementLabel?.Label;
        if (label != null && state.Peek() is Word word && string.Equals(word.Value, label.Name, StringComparison.OrdinalIgnoreCase))
        {
            state.Next();
            return label;
        }
        return null;
    }

    private Identifier? ParsePgEndLabel(ParserState state, string expected)
    {
        if (state.Peek() is not Word || state.StatementLabel?.Dialect != StatementLabelDialect.Pg)
        {
            return null;
        }
        Token token = state.Peek();
        Identifier endLabel = _parseIdentifier(state);
        Identifier startLabel = state.StatementLabel.Value.Label;
        string startName = startLabel.QuoteStyle == QuoteStyle.None ? startLabel.Name.ToLowerInvariant() : startLabel.Name;
        string endName = endLabel.QuoteStyle == QuoteStyle.None ? endLabel.Name.ToLowerInvariant() : endLabel.Name;
        if (!string.Equals(startName, endName, StringComparison.Ordinal))
        {
            throw ParserState.ExpectedException(expected, token);
        }
        return endLabel;
    }

    public virtual Statement ParseIf(ParserState state)
    {
        if (state.PgPl && state.TryParse(s => ParseMyOrPgIf(s, true), out Statement? pgStmt))
        {
            return pgStmt;
        }
        if (state.My && !state.PgPl && state.TryParse(s => ParseMyOrPgIf(s, false), out Statement? myStmt))
        {
            return myStmt;
        }
        if (state.Ms && state.TryParse(s => ParseMsIf(s), out Statement? msStmt))
        {
            return msStmt;
        }
        throw new ParseException.DialectRequired("IF syntax", state.Peek().Location);
    }

    protected Statement ParseMyOrPgIf(ParserState state, bool elsIf)
    {
        // Keyword elseIfKeyword = elsIf ? Keyword.ELSIF : Keyword.ELSEIF;
        // string elseIfKeywordText = elsIf ? "ELSIF" : "ELSEIF";
        Keyword[] elseIfKeywords = elsIf
            ? [Keyword.ELSEIF, Keyword.ELSIF]
            : [Keyword.ELSEIF];

        var ifEnder = new EndSubstatements()
        {
            RequireTerminatorBeforeEnd = state.PgPl,
            PreserveEndTrivia = token => PreserveConditionalEndTrivia(token, !state.PgPl),
            GetFinished = s =>
            {
                if (s.PeekKeywordsAll(Keyword.END, Keyword.IF))
                {
                    return true;
                }
                if (s.PeekKeyword(Keyword.ELSEIF))
                {
                    return true;
                }
                if (elsIf && s.PeekKeyword(Keyword.ELSIF))
                {
                    return true;
                }
                if (s.PeekKeyword(Keyword.ELSE))
                {
                    return true;
                }
                return false;
            },
            GetEofException = s => s.ExpectedException(elsIf
                ? ["ELSEIF", "ELSIF", "ELSE", "END IF"]
                : ["ELSEIF", "ELSE", "END IF"])
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
            Token start = state.Peek();
            Keyword endingKeyword = state.ParseKeywordsAny(elseIfKeywords);
            if (endingKeyword == Keyword.undefined)
            {
                break;
            }
            bool useElsIf = endingKeyword == Keyword.ELSIF;

            Expression elseIfCondition = _parseExpr(state);

            state.ExpectKeyword(Keyword.THEN);

            SqlValueList<Statement> elseIfStatements = _p.Parse(state, ifEnder);

            var elseIf = new If.ElseIf(elseIfCondition, elseIfStatements, useElsIf);
            Parser.AttachStatementMetadata(elseIf, start, state);
            elseIfBlocks.Add(elseIf);
        }

        if (state.ParseKeywordsAll(Keyword.END, Keyword.IF))
        {
            return new If.My(ifExpression, ifStatements)
            {
                ElseIfs = elseIfBlocks
            };
        }

        state.ExpectKeyword(Keyword.ELSE);
        SqlValueList<Statement> elseStatements = _p.Parse(state, new EndSubstatements()
        {
            RequireTerminatorBeforeEnd = state.PgPl,
            PreserveEndTrivia = token => PreserveConditionalEndTrivia(token, !state.PgPl),
            GetFinished = s => s.PeekKeywordsAll(Keyword.END, Keyword.IF),
            GetEofException = s => s.ExpectedException("END IF")
        });
        state.ExpectKeywordsAll(Keyword.END, Keyword.IF);

        return new If.My(ifExpression, ifStatements)
        {
            ElseIfs = elseIfBlocks,
            ElseStatements = elseStatements
        };
    }

    protected Statement ParseMsIf(ParserState state)
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

    public Statement ParseCase(ParserState state)
    {
        state.ExpectKeyword(Keyword.CASE);

        if (state.PeekKeyword(Keyword.WHEN))
        {
            return ParseSearchedCase(state);
        }
        else
        {
            return ParseSimpleCase(state, state.PgPl);
        }
    }

    private SearchedCase ParseSearchedCase(ParserState state)
    {
        SqlValueList<CaseItem> items = [];
        do
        {
            items.Add(ParseCaseItem(state, false));
        }
        while (state.PeekKeyword(Keyword.WHEN));
        SqlValueList<Statement>? elseStatements = null;

        if (state.ParseKeyword(Keyword.ELSE))
        {
            elseStatements = _p.Parse(state, new EndSubstatements()
            {
                RequireTerminatorBeforeEnd = state.PgPl,
                PreserveEndTrivia = token => PreserveConditionalEndTrivia(token, !state.PgPl),
                GetFinished = s => s.PeekKeywordsAll(Keyword.END, Keyword.CASE),
                GetEofException = s => s.ExpectedException("END CASE")
            });
        }
        state.ExpectKeywordsAll(Keyword.END, Keyword.CASE);
        return new SearchedCase(items, elseStatements);
    }

    private SimpleCase ParseSimpleCase(ParserState state, bool multiCondition)
    {
        Expression searchExpression = _parseExpr(state);
        SqlValueList<CaseItem> items = [];
        do
        {
            items.Add(ParseCaseItem(state, multiCondition));
        }
        while (state.PeekKeyword(Keyword.WHEN));

        SqlValueList<Statement>? elseStatements = null;
        if (state.ParseKeyword(Keyword.ELSE))
        {
            elseStatements = _p.Parse(state, new EndSubstatements()
            {
                RequireTerminatorBeforeEnd = state.PgPl,
                PreserveEndTrivia = token => PreserveConditionalEndTrivia(token, !state.PgPl),
                GetFinished = s => s.PeekKeywordsAll(Keyword.END, Keyword.CASE),
                GetEofException = s => s.ExpectedException("END CASE")
            });
        }
        state.ExpectKeywordsAll(Keyword.END, Keyword.CASE);
        return new SimpleCase(searchExpression, items, elseStatements);
    }

    private CaseItem ParseCaseItem(ParserState state, bool multiCondition)
    {
        Token start = state.Peek();
        state.ExpectKeyword(Keyword.WHEN);
        SqlValueList<Expression> conditions = [];
        if (multiCondition)
        {
            conditions = state.ParseCommaSeparated(_parseExpr);
        }
        else
        {
            conditions.Add(_parseExpr(state));
        }

        state.ExpectKeyword(Keyword.THEN);
        SqlValueList<Statement> statements = _p.Parse(state, new EndSubstatements()
        {
            RequireTerminatorBeforeEnd = state.PgPl,
            PreserveEndTrivia = token => PreserveConditionalEndTrivia(token, !state.PgPl),
            GetFinished = s =>
            {
                if (state.PeekKeyword(Keyword.WHEN))
                {
                    return true;
                }
                if (state.PeekKeyword(Keyword.ELSE))
                {
                    return true;
                }
                if (state.PeekKeywordsAll(Keyword.END, Keyword.CASE))
                {
                    return true;
                }

                return false;
            },
            GetEofException = s => s.ExpectedException("WHEN", "ELSE", "END CASE")
        });
        var item = new CaseItem(conditions, statements);
        if (start.PreNonSql is { } trivia && trivia.SafeAny())
        {
            item.Meta.PreNonSql = trivia.Items;
        }
        return item;
    }

    private static bool PreserveConditionalEndTrivia(Token token, bool preserveWhitespace)
    {
        return token is Word { Keyword: Keyword.ELSE or Keyword.END }
            && (preserveWhitespace || token.PreNonSql is { } trivia && trivia.Items.Any(item => item is NonSql.Comment));
    }

    public virtual BeginAtomic ParseBeginAtomic(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.BEGIN, Keyword.ATOMIC);
        var ender = new EndSubstatements
        {
            RequireTerminatorBeforeEnd = true,
            GetFinished = innerState => innerState.ParseKeyword(Keyword.END),
            GetEofException = innerState => innerState.ExpectedException("END")
        };
        SqlValueList<Statement> statements = _p.Parse(state, ender);
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
