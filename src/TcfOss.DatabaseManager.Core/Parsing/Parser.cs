using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class Parser : IParser
{
    public virtual QuoteStyle DefaultQuoteStyle => QuoteStyle.Ansi;

    public ComponentParser ComponentParser { get; protected init; }
    public ExpressionParser ExpressionParser { get; protected init; }
    public ValueParser ValueParser { get; protected init; }
    public DataTypeParser DataTypeParser { get; protected init; }
    public SelectParser SelectParser { get; protected init; }
    public DmlParser DmlParser { get; protected init; }
    public DdlParser DdlParser { get; protected init; }
    public TableParser TableParser { get; protected init; }
    public ControlFlowParser ControlFlowParser { get; protected init; }

    public PrecedenceManager PrecedenceManager { get; protected init; }
    public virtual bool SupportsPostgresCastSyntax => true;
    public virtual bool SupportsPostgresRegexOperators => true;
    public virtual bool SupportsPostgresIdentitySyntax => true;
    public virtual bool SupportsPostgresColumnCollationSyntax => true;
    public virtual bool SupportsPostgresExpressionCollationSyntax => true;
    public virtual bool SupportsPostgresArrayAndRowExpressionSyntax => true;
    public virtual bool SupportsPostgresOperatorSyntax => true;
    public virtual bool SupportsPostgresIntervalLiteralSyntax => true;
    public virtual bool SupportsMySqlIntervalExpressionSyntax => true;
    protected virtual bool SupportsCommentOn => true;
    public Func<string, bool> IsBuiltInFunction { get; protected init; }
    public Func<string, bool> AllowsFunctionCallWithoutParentheses { get; protected init; }

    public Parser()
    {
        PrecedenceManager = new PrecedenceManager();
        IsBuiltInFunction = new FunctionNameProvider().IsBuiltInFunction;
        AllowsFunctionCallWithoutParentheses = FunctionNameProvider.AllowsFunctionCallWithoutParentheses;
        ValueParser = new ValueParser();
        DataTypeParser = new DataTypeParser(this);
        ComponentParser = new ComponentParser(this);
        ExpressionParser = new ExpressionParser(this);
        SelectParser = new SelectParser(this);
        DmlParser = new DmlParser(this);
        DdlParser = new DdlParser(this);
        TableParser = new TableParser(this);
        ControlFlowParser = new ControlFlowParser(this);
    }

    public virtual SqlValueList<Statement> Parse(Token[] tokens, int sourceId = 0)
    {
        var state = new ParserState(tokens, sourceId);
        return Parse(state, null);
    }

    public virtual SqlValueList<Statement> Parse(ParserState state, EndSubstatements? endChecker)
    {
        SqlValueList<Statement> statements = [];
        bool expectingTerminator = false;

        while (true)
        {
            while (state.ConsumeTokenIs<Semicolon>())
            {
                expectingTerminator = false;
            }

            Token next = state.Peek();
            if (next is Eof)
            {
                if (endChecker != null)
                {
                    throw endChecker.GetEofException(state);
                }
                if (next.PreNonSql.SafeAny())
                {
                    statements.Add(next.PreNonSql.ToInertOnly());
                }
                break;
            }

            if (expectingTerminator && endChecker is { RequireTerminatorBeforeEnd: true })
            {
                throw ParserState.ExpectedException("end of statement", next);
            }

            if (endChecker?.GetFinished(state) ?? false)
            {
                break;
            }

            if (expectingTerminator)
            {
                throw ParserState.ExpectedException("end of statement", next);
            }

            Statement statement = ParseStatement(state);
            statements.Add(statement);
            expectingTerminator = true;
        }

        return statements;
    }

    public virtual Statement ParseStatement(ParserState state)
    {
        Token token = state.Peek();

        Word? labelToken = null;
        if (token is Word { Sigil: SigilKind.None } word && state.PeekNthIs<Colon>(1))
        {
            labelToken = word;
            state.Next();
            state.Next();
            token = state.Peek();
        }

        using ParserState.StatementScope scope = state.EnterStatementScope(labelToken);

        Statement statement = ParseNormalStatement(state, token);
        AttachStatementMetadata(statement, token, state);
        return AttachLabelToStatement(statement, state);
    }

    protected virtual Statement ParseNormalStatement(ParserState state, Token token)
    {
        switch (token)
        {
            case Word word:
                return ParseKeywordStatement(state, word);
            case ParenOpen:
                return SelectParser.ParseSelect(state, null);
            case StringLiteral literal when state.Context.HasFlag(ParseContext.StringLiteralAsStatement):
                state.Next();
                return new RoutineBodyLiteral(literal.Value.Replace("''", "'", StringComparison.Ordinal));
            case DollarQuotedStringLiteral literal when state.Context.HasFlag(ParseContext.StringLiteralAsStatement):
                state.Next();
                return new RoutineBodyLiteral(literal.Value) { DollarQuoteTag = literal.Tag };
            case EscapeStringLiteral when state.Context.HasFlag(ParseContext.StringLiteralAsStatement):
                return new RoutineBodyLiteral(((Value.EscapeString)ValueParser.ParseValue(state)).DecodedValue);
            case UnicodeStringLiteral when state.Context.HasFlag(ParseContext.StringLiteralAsStatement):
                return new RoutineBodyLiteral(((Value.UnicodeString)ValueParser.ParseValue(state)).DecodedValue);
            default:
                throw ParserState.ExpectedException("SQL statement".Italic(), token);
        }
    }

    protected virtual Statement ParseKeywordStatement(ParserState state, Word first)
    {
#pragma warning disable IDE0072 // Add missing cases
        return first.Keyword switch
        {
            Keyword.SELECT or Keyword.VALUES => SelectParser.ParseSelect(state, null),
            Keyword.INSERT => DmlParser.ParseInsert(state, null),
            Keyword.UPDATE => DmlParser.ParseUpdate(state, null),
            Keyword.DELETE => DmlParser.ParseDelete(state, null),
            Keyword.WITH => ParseCteQuery(state),
            Keyword.CREATE => ParseCreate(state),
            Keyword.COMMENT when SupportsCommentOn => DdlParser.ParseCommentOn(state),
            Keyword.ALTER => ParseAlter(state),
            Keyword.DROP => DdlParser.ParseDropObject(state),
            Keyword.IF => ControlFlowParser.ParseIf(state),
            Keyword.DECLARE => ParseDeclare(state),
            Keyword.SET => ParseSet(state),
            Keyword.BEGIN => ParseBegin(state),
            Keyword.RETURN => ParseReturn(state),
            Keyword.USE => ParseUse(state),
            Keyword.LEAVE => ParseLeave(state),
            Keyword.ITERATE => ParseIterate(state),
            Keyword.FETCH => ParseFetch(state),
            Keyword.OPEN => ParseOpen(state),
            Keyword.CLOSE => ParseClose(state),
            Keyword.TRUNCATE => DdlParser.ParseTruncateTable(state),
            _ => throw ParserState.ExpectedException("SQL statement".Italic(), first)
        };
#pragma warning restore IDE0072 // Add missing cases
    }

    private Use ParseUse(ParserState state)
    {
        state.ExpectKeyword(Keyword.USE);

        UseObject? useObjectType = state.Peek() switch
        {
            Word { Keyword: Keyword.CATALOG } => UseObject.Catalog,
            Word { Keyword: Keyword.DATABASE } => UseObject.Database,
            Word { Keyword: Keyword.SCHEMA } => UseObject.Schema,
            _ => null
        };

        if (useObjectType != null)
        {
            _ = state.Next();
        }

        ObjectName useObjectName = ComponentParser.ParseObjectName(state);

        return new Use(useObjectName) { UseObject = useObjectType };
    }

    private Leave ParseLeave(ParserState state)
    {
        state.ExpectKeyword(Keyword.LEAVE);
        Identifier thingToLeave = ComponentParser.ParseIdentifier(state);
        return new Leave(thingToLeave);
    }

    private Iterate ParseIterate(ParserState state)
    {
        state.ExpectKeyword(Keyword.ITERATE);
        Identifier thingToIterate = ComponentParser.ParseIdentifier(state);
        return new Iterate(thingToIterate);
    }

    private Open ParseOpen(ParserState state)
    {
        state.ExpectKeyword(Keyword.OPEN);
        Identifier thingToOpen = ComponentParser.ParseIdentifier(state);
        return new Open(thingToOpen);
    }

    private Close ParseClose(ParserState state)
    {
        state.ExpectKeyword(Keyword.CLOSE);
        Identifier thingToClose = ComponentParser.ParseIdentifier(state);
        return new Close(thingToClose);
    }

    private Statement ParseCteQuery(ParserState state)
    {
        CommonTableExpression cte = SelectParser.ParseCommonTableExpression(state);

        Statement statement = state.Peek() switch
        {
            Word { Keyword: Keyword.SELECT } => SelectParser.ParseSelect(state, cte),
            Word { Keyword: Keyword.INSERT } => DmlParser.ParseInsert(state, cte),
            Word { Keyword: Keyword.UPDATE } => DmlParser.ParseUpdate(state, cte),
            Word { Keyword: Keyword.DELETE } => DmlParser.ParseDelete(state, cte),
            ParenOpen => SelectParser.ParseSelect(state, cte),
            _ => throw state.ExpectedException("SELECT", "INSERT", "UPDATE", "DELETE")
        };

        return statement;
    }

    protected virtual CreateObjectOptions ParseCreateObjectOptions(ParserState state)
    {
        CreateOrLabel? createOrLabel = ComponentParser.ParseCreateOrLabel(state);

        ViewAlgorithm? viewAlgorithm = DdlParser.ParseViewAlgorithm(state);
        Definer? definer = ComponentParser.ParseOptionalDefiner(state);
        SecurityContext? securityContext = DdlParser.ParseSecurityContext(state);
        bool aggregate = state.ParseKeyword(Keyword.AGGREGATE);
        bool temporary = state.ParseKeywordsAny(Keyword.TEMPORARY, Keyword.TEMP) != Keyword.undefined;

        return new CreateObjectOptions
        {
            CreateOrLabel = createOrLabel,
            ViewAlgorithm = viewAlgorithm,
            Definer = definer,
            SecurityContext = securityContext,
            Aggregate = aggregate,
            Temporary = temporary
        };
    }

    protected virtual void GuardCreateOptions(ParserState state, Word objectTypeToken, CreateObjectOptions options)
    {
        Keyword kw = objectTypeToken.Keyword;
        if (options.ViewAlgorithm != null && kw != Keyword.VIEW)
        {
            throw state.ExpectedException("VIEW");
        }
        if (options.Definer != null && kw is not (Keyword.VIEW or Keyword.PROCEDURE or Keyword.FUNCTION or Keyword.TRIGGER or Keyword.EVENT))
        {
            throw state.ExpectedException("VIEW", "PROCEDURE", "FUNCTION", "TRIGGER", "EVENT");
        }
        if (options.SecurityContext != null && kw is not (Keyword.VIEW or Keyword.PROCEDURE or Keyword.FUNCTION))
        {
            throw state.ExpectedException("VIEW", "PROCEDURE", "FUNCTION");
        }
        if (options.Aggregate && kw is not Keyword.FUNCTION)
        {
            throw state.ExpectedException("FUNCTION");
        }
        if (options.Temporary && kw is not Keyword.TABLE)
        {
            throw state.ExpectedException("TABLE");
        }
        if (options.CreateOrLabel != null && kw is not (Keyword.TABLE or Keyword.VIEW or Keyword.TRIGGER or Keyword.CONSTRAINT or Keyword.FUNCTION or Keyword.PROCEDURE or Keyword.EVENT or Keyword.INDEX))
        {
            throw state.ExpectedException("TABLE", "VIEW", "TRIGGER", "FUNCTION", "PROCEDURE", "EVENT", "INDEX");
        }
    }

    protected virtual Statement ParseCreate(ParserState state)
    {
        state.ExpectKeyword(Keyword.CREATE);
        CreateObjectOptions options = ParseCreateObjectOptions(state);

        if (state.Peek() is not Word objectTypeWord)
        {
            throw state.ExpectedException(options.AllSupportedTypes);
        }

        GuardCreateOptions(state, objectTypeWord, options);

        return objectTypeWord.Keyword switch
        {
            Keyword.DOMAIN => DdlParser.ParseCreateDomain(state),
            Keyword.TYPE => DdlParser.ParseCreateType(state),
            Keyword.TABLE => TableParser.ParseCreate(state, options.CreateOrLabel == CreateOrLabel.OrReplace, options.Temporary),
            Keyword.TRIGGER or Keyword.CONSTRAINT => DdlParser.ParseCreateTrigger(state, options.CreateOrLabel, options.Definer),
            Keyword.FUNCTION => DdlParser.ParseCreateFunction(state, options.Aggregate, options.CreateOrLabel, options.Definer),
            Keyword.PROCEDURE or Keyword.PROC => DdlParser.ParseCreateProcedure(state, options.CreateOrLabel, options.Definer),
            Keyword.VIEW => DdlParser.ParseCreateView(state, options.CreateOrLabel, options.Definer, options.ViewAlgorithm, options.SecurityContext),
            Keyword.EVENT => DdlParser.ParseCreateEvent(state, options.CreateOrLabel, options.Definer),
            Keyword.INDEX or Keyword.UNIQUE or Keyword.FULLTEXT or Keyword.SPATIAL or Keyword.CLUSTERED or Keyword.NONCLUSTERED => DdlParser.ParseCreateIndex(state, options.CreateOrLabel),
            _ => throw state.ExpectedException(options.AllSupportedTypes)
        };
    }

    private AlterTable ParseAlter(ParserState state)
    {
        state.ExpectKeyword(Keyword.ALTER);

        return state.Peek() switch
        {
            Word { Keyword: Keyword.TABLE } => TableParser.ParseAlter(state),
            _ => throw state.ExpectedException("TABLE", "PROCEDURE", "VIEW")
        };
    }

    protected virtual Statement ParseBegin(ParserState state)
    {
        if (state.PeekKeywordsAllEqual(Keyword.BEGIN, Keyword.ATOMIC))
        {
            if (!state.Context.HasFlag(ParseContext.BeginAtomic))
            {
                throw state.ExpectedException("SQL statement");
            }
            using (state.EnterContext(disable: ParseContext.BeginAtomic))
            {
                return ControlFlowParser.ParseBeginAtomic(state);
            }
        }
        return ControlFlowParser.ParseBeginEnd(state);
    }


    private Statement ParseDeclare(ParserState state)
    {
        state.ExpectKeyword(Keyword.DECLARE, false);
        Token upcoming1 = state.PeekNth(1);
        Token upcoming2 = state.PeekNth(2);

        if (upcoming1 is Word { Keyword: Keyword.CONTINUE or Keyword.EXIT or Keyword.UNDO })
        {
            return ControlFlowParser.ParseDeclareConditionHandler(state);
        }
        if (upcoming2 is Word { Keyword: Keyword.CONDITION } && state.PeekNth(3) is Word { Keyword: Keyword.FOR })
        {
            return ControlFlowParser.ParseDeclareCondition(state);
        }
        if (upcoming2 is Word { Keyword: Keyword.CURSOR })
        {
            return ControlFlowParser.ParseDeclareCursor(state);
        }
        return ControlFlowParser.ParseDeclareLocalVariable(state);
    }

    protected virtual Fetch ParseFetch(ParserState state)
    {
        state.ExpectKeyword(Keyword.FETCH);
        Fetch.FromCursorInto.FromLabelOption? labelOption = null;
        if (state.ParseKeywordsAll(Keyword.NEXT, Keyword.FROM))
        {
            labelOption = Fetch.FromCursorInto.FromLabelOption.NextFrom;
        }
        else if (state.ParseKeyword(Keyword.FROM))
        {
            labelOption = Fetch.FromCursorInto.FromLabelOption.From;
        }
        Identifier cursorName = ComponentParser.ParseIdentifier(state);
        state.ExpectKeyword(Keyword.INTO);
        SqlValueList<Identifier> intoVars = state.ParseCommaSeparated(ComponentParser.ParseIdentifier);
        return new Fetch.FromCursorInto(cursorName, intoVars)
        {
            FromLabel = labelOption
        };
    }

    private SetVariable ParseSet(ParserState state)
    {
        return ControlFlowParser.ParseSetVariable(state);
    }

    protected virtual Return ParseReturn(ParserState state)
    {
        state.ExpectKeyword(Keyword.RETURN);
        return new Return(ExpressionParser.ParseExpr(state));
    }

    internal static void AttachStatementMetadata(Statement statement, Token token, ParserState state)
    {
        if (token.PreNonSql.SafeAny())
        {
            statement.Meta.PreNonSql = token.PreNonSql.Items;
            statement.Meta.Start = token.PreNonSql.Start;
        }
        else
        {
            statement.Meta.Start = token.Location.Position;
        }


        Token upcoming = state.Peek();
        if (upcoming is Semicolon or Eof && upcoming.PreNonSql.SafeAny())
        {
            statement.Meta.PostNonSql = upcoming.PreNonSql.Items;
        }

        int end = upcoming.Location.Position;
        int endPlusTerminatorLength = end;
        if (upcoming is Semicolon || upcoming is MyTerminator)
        {
            endPlusTerminatorLength += upcoming.Length;
        }
        statement.Meta.End = end;
        statement.Meta.EndPlusTerminatorLength = endPlusTerminatorLength;
    }

    private static Statement AttachLabelToStatement(Statement statement, ParserState state)
    {
        if (state.StatementLabel is null)
        {
            return statement;
        }
        StatementLabel statementLabel = state.StatementLabel.Value;
        statement = new LabeledStatement(statement, statementLabel.Label)
        {
            Meta = new()
            {
                End = statement.Meta.End,
                EndPlusTerminatorLength = statement.Meta.EndPlusTerminatorLength
            }
        };
        if (statementLabel.PreNonSql.SafeAny())
        {
            statement.Meta.PreNonSql = statementLabel.PreNonSql.Items;
            statement.Meta.Start = statementLabel.PreNonSql.Start;
        }
        else
        {
            statement.Meta.Start = statementLabel.StartPosition;
        }
        return statement;
    }
}
