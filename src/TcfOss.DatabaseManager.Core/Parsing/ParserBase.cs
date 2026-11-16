using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Parsing;

public abstract class ParserBase : IParser
{
    public virtual Grammar Grammar => Grammar.All;

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
    public virtual bool SupportsPostgresIdentitySyntax => true;
    public virtual bool SupportsPostgresColumnCollationSyntax => true;
    public virtual bool SupportsPostgresColumnAlterSyntax => true;
    public virtual bool SupportsPostgresTableCreateSyntax => true;
    public virtual bool SupportsPostgresConstraintSyntax => true;
    public virtual bool SupportsPostgresTableAlterSyntax => true;
    public virtual bool SupportsDefaultExpressionsWithoutParentheses => true;
    public virtual bool SupportsPostgresExpressionCollationSyntax => true;
    public virtual bool SupportsPostgresArrayAndRowExpressionSyntax => true;
    public virtual bool SupportsPostgresOperatorSyntax => true;
    public Func<string, bool> IsBuiltInFunction { get; protected init; }
    public Func<string, bool> AllowsFunctionCallWithoutParentheses { get; protected init; }

    public ParserBase()
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
        var state = new ParserState(Grammar, tokens, sourceId);
        return Parse(state, null);
    }

    public abstract SqlValueList<Statement> Parse(ParserState state, EndSubstatements? endChecker);

    public virtual Statement ParseStatement(ParserState state)
    {
        StatementLabel? label = MaybeParseStatementLabel(state);
        using ParserState.StatementScope scope = state.EnterStatementScope(label);

        Token token = state.Peek();

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
            Keyword.PERFORM when state.PgPl => SelectParser.ParsePgPerform(state),
            Keyword.INSERT => DmlParser.ParseInsert(state, null),
            Keyword.UPDATE => DmlParser.ParseUpdate(state, null),
            Keyword.DELETE => DmlParser.ParseDelete(state, null),
            Keyword.WITH => ParseCteQuery(state),
            Keyword.CREATE => ParseCreate(state),
            Keyword.COMMENT when state.PgAny => DdlParser.ParseCommentOn(state),
            Keyword.ALTER => ParseAlter(state),
            Keyword.DROP => DdlParser.ParseDropObject(state),
            Keyword.IF when state.My || state.PgPl || state.Ms => ControlFlowParser.ParseIf(state),
            Keyword.CASE when state.My || state.PgPl => ControlFlowParser.ParseCase(state),
            Keyword.LOOP when state.My || state.PgPl => ControlFlowParser.ParseLoop(state),
            Keyword.WHILE when state.My || state.PgPl || state.Ms => ControlFlowParser.ParseWhile(state),
            Keyword.FOR when state.PgPl => ControlFlowParser.ParsePgFor(state),
            Keyword.FOREACH when state.PgPl => ControlFlowParser.ParsePgForeach(state),
            Keyword.EXIT when state.PgPl => ControlFlowParser.ParsePgLoopExit(state),
            Keyword.CONTINUE when state.PgPl => ControlFlowParser.ParsePgLoopContinue(state),
            Keyword.RAISE when state.PgPl => ControlFlowParser.ParsePgRaise(state),
            Keyword.ASSERT when state.PgPl => ControlFlowParser.ParsePgAssert(state),
            Keyword.GET when state.PgPl => ControlFlowParser.ParsePgGetDiagnostics(state),
            Keyword.EXECUTE when state.PgPl && state.Context.HasFlag(ParseContext.PgProceduralBlock) => ControlFlowParser.ParsePgExecute(state),
            Keyword.COMMIT when !state.Context.HasFlag(ParseContext.BeginAtomicBody) => ControlFlowParser.ParseCommit(state),
            Keyword.ROLLBACK when !state.Context.HasFlag(ParseContext.BeginAtomicBody) => ControlFlowParser.ParseRollback(state),
            Keyword.MOVE when state.Pg || (state.PgPl && state.Context.HasFlag(ParseContext.PgProceduralBlock)) => ControlFlowParser.ParsePgMove(state),
            Keyword.DECLARE => ParseDeclare(state),
            Keyword.NULL when state.PgPl && state.Context.HasFlag(ParseContext.PgProceduralBlock) => ParsePgNull(state),
            Keyword.SET => ParseSet(state),
            Keyword.BEGIN => ParseBegin(state),
            Keyword.RETURN => ControlFlowParser.ParseReturn(state),
            Keyword.CALL => ParseCall(state),
            Keyword.USE => ParseUse(state),
            Keyword.LEAVE => ParseLeave(state),
            Keyword.ITERATE => ParseIterate(state),
            Keyword.FETCH when state.PgPl && state.Context.HasFlag(ParseContext.PgProceduralBlock) => ControlFlowParser.ParsePgFetch(state),
            Keyword.FETCH => ParseFetch(state),
            Keyword.OPEN when state.PgPl && state.Context.HasFlag(ParseContext.PgProceduralBlock) => ControlFlowParser.ParsePgOpen(state),
            Keyword.OPEN => ParseOpen(state),
            Keyword.CLOSE => ParseClose(state),
            Keyword.TRUNCATE => DdlParser.ParseTruncateTable(state),
            Keyword.REFRESH => DdlParser.ParseRefreshMaterializedView(state),
            _ => throw ParserState.ExpectedException("SQL statement".Italic(), first)
        };
#pragma warning restore IDE0072 // Add missing cases
    }

    protected virtual StatementLabel? MaybeParseStatementLabel(ParserState state)
    {
        if (state.PgPl)
        {
            StatementLabel? label = MaybeParsePgStatementLabel(state);
            if (label != null)
            {
                return label;
            }
        }
        return state.My ? MaybeParseMyStatementLabel(state) : null;
    }

    protected static StatementLabel? MaybeParseMyStatementLabel(ParserState state)
    {
        if (state.Peek() is Word { Sigil: SigilKind.None } word && state.PeekNthIs<Colon>(1))
        {
            state.Next();
            state.Next();
            return new StatementLabel(
                word.ToIdentifier(state.SourceId),
                word.Location.Position,
                StatementLabelDialect.My,
                word.PreNonSql,
                null,
                null
            );
        }
        return null;
    }

    protected StatementLabel? MaybeParsePgStatementLabel(ParserState state)
    {
        if (!state.PgPl || state.Peek() is not OperatorToken { Value: "<<" })
        {
            return null;
        }
        Token first = state.Next();
        if (state.Peek() is not Word token)
        {
            throw state.ExpectedException("block_label");
        }
        Identifier label = ComponentParser.ParseIdentifier(state, "block_label");
        if (state.Peek() is not OperatorToken { Value: ">>" } closing)
        {
            throw state.ExpectedException(">>");
        }
        state.Next(); // Consume the closing ">>" operator token
        if (state.Peek() is not Word { Keyword: Keyword.BEGIN or Keyword.DECLARE or Keyword.LOOP or Keyword.WHILE or Keyword.FOR or Keyword.FOREACH })
        {
            throw state.ExpectedException("BEGIN", "DECLARE", "LOOP", "WHILE", "FOR", "FOREACH");
        }

        return new StatementLabel(
            label,
            first.Location.Position,
            StatementLabelDialect.Pg,
            first.PreNonSql,
            token.PreNonSql,
            closing.PreNonSql
        );
    }

    private static PgNull ParsePgNull(ParserState state)
    {
        state.ExpectKeyword(Keyword.NULL);
        return new PgNull();
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
            Word { Keyword: Keyword.SELECT or Keyword.VALUES } => SelectParser.ParseSelect(state, cte),
            Word { Keyword: Keyword.INSERT } => DmlParser.ParseInsert(state, cte),
            Word { Keyword: Keyword.UPDATE } => DmlParser.ParseUpdate(state, cte),
            Word { Keyword: Keyword.DELETE } => DmlParser.ParseDelete(state, cte),
            ParenOpen => SelectParser.ParseSelect(state, cte),
            _ => throw state.ExpectedException("SELECT", "VALUES", "INSERT", "UPDATE", "DELETE")
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
        TemporaryLabel? temporaryLabel = ParseTemporaryLabel(state);
        bool unlogged = temporaryLabel == null && state.ParseKeyword(Keyword.UNLOGGED);
        bool recursive = state.ParseKeyword(Keyword.RECURSIVE);

        return new CreateObjectOptions
        {
            CreateOrLabel = createOrLabel,
            ViewAlgorithm = viewAlgorithm,
            Definer = definer,
            SecurityContext = securityContext,
            Aggregate = aggregate,
            TemporaryLabel = temporaryLabel,
            Recursive = recursive,
            Unlogged = unlogged
        };
    }

    protected virtual TemporaryLabel? ParseTemporaryLabel(ParserState state)
    {
        if (state.ParseKeyword(Keyword.TEMPORARY))
        {
            return TemporaryLabel.Temporary;
        }
        if (state.ParseKeyword(Keyword.TEMP))
        {
            return TemporaryLabel.Temp;
        }
        return null;
    }

    protected virtual void GuardCreateOptions(ParserState state, Word objectTypeToken, CreateObjectOptions options)
    {
        Keyword kw = objectTypeToken.Keyword;
        if (options.Recursive && kw != Keyword.VIEW)
        {
            throw state.ExpectedException("VIEW");
        }
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
        if (options.Unlogged && kw is not (Keyword.TABLE or Keyword.SEQUENCE))
        {
            throw state.ExpectedException("TABLE", "SEQUENCE");
        }
        if (options.TemporaryLabel != null && kw is not (Keyword.TABLE or Keyword.VIEW or Keyword.SEQUENCE))
        {
            throw state.ExpectedException("TABLE", "VIEW", "SEQUENCE");
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
            Keyword.SEQUENCE => DdlParser.ParseCreateSequence(state, options.TemporaryLabel, options.Unlogged),
            Keyword.TYPE => DdlParser.ParseCreateType(state),
            Keyword.TABLE => TableParser.ParseCreate(state, options.CreateOrLabel == CreateOrLabel.OrReplace, options.TemporaryLabel, options.Unlogged),
            Keyword.TRIGGER or Keyword.CONSTRAINT => DdlParser.ParseCreateTrigger(state, options.CreateOrLabel, options.Definer),
            Keyword.FUNCTION => DdlParser.ParseCreateFunction(state, options.Aggregate, options.CreateOrLabel, options.Definer),
            Keyword.PROCEDURE or Keyword.PROC => DdlParser.ParseCreateProcedure(state, options.CreateOrLabel, options.Definer),
            Keyword.VIEW => DdlParser.ParseCreateView(state, options.CreateOrLabel, options.Definer, options.ViewAlgorithm, options.SecurityContext, options.TemporaryLabel, options.Recursive),
            Keyword.MATERIALIZED => DdlParser.ParseCreateMaterializedView(state),
            Keyword.EVENT => DdlParser.ParseCreateEvent(state, options.CreateOrLabel, options.Definer),
            Keyword.INDEX or Keyword.UNIQUE or Keyword.FULLTEXT or Keyword.SPATIAL or Keyword.CLUSTERED or Keyword.NONCLUSTERED => DdlParser.ParseCreateIndex(state, options.CreateOrLabel),
            _ => throw state.ExpectedException(options.AllSupportedTypes)
        };
    }

    protected virtual Statement ParseAlter(ParserState state)
    {
        state.ExpectKeyword(Keyword.ALTER);

        return state.Peek() switch
        {
            Word { Keyword: Keyword.TABLE } => TableParser.ParseAlter(state),
            Word { Keyword: Keyword.MATERIALIZED } => DdlParser.ParseAlterMaterializedView(state),
            Word { Keyword: Keyword.SEQUENCE } => DdlParser.ParseAlterSequence(state),
            Word { Keyword: Keyword.FUNCTION or Keyword.PROCEDURE } => DdlParser.ParseAlterRoutine(state),
            _ => throw state.ExpectedException("TABLE", "FUNCTION", "PROCEDURE", "VIEW", "MATERIALIZED VIEW", "SEQUENCE")
        };
    }

    protected virtual Statement ParseBegin(ParserState state)
    {
        if (state.PeekKeywordsAll(Keyword.BEGIN, Keyword.ATOMIC))
        {
            if (!state.Context.HasFlag(ParseContext.ExpectBeginAtomic))
            {
                throw state.ExpectedException("SQL statement");
            }
            using (state.EnterContext(ParseContext.BeginAtomicBody, ParseContext.ExpectBeginAtomic))
            {
                return ControlFlowParser.ParseBeginAtomic(state);
            }
        }
        if (state.PgAny)
        {
            return ControlFlowParser.ParsePgPlBeginEnd(state);
        }
        return ControlFlowParser.ParseBeginEnd(state);
    }


    protected virtual Statement ParseDeclare(ParserState state)
    {
        state.ExpectKeyword(Keyword.DECLARE, false);

        if (state.My && state.PeekN(4) is [Word { Keyword: Keyword.DECLARE }, Word { Keyword: Keyword.CONTINUE or Keyword.EXIT or Keyword.UNDO }, Word { Keyword: Keyword.HANDLER }, Word { Keyword: Keyword.FOR }])
        {
            return ControlFlowParser.ParseDeclareConditionHandler(state);
        }
        if (state.My && state.PeekN(4) is [Word { Keyword: Keyword.DECLARE }, _, Word { Keyword: Keyword.CONDITION }, Word { Keyword: Keyword.FOR }])
        {
            return ControlFlowParser.ParseDeclareCondition(state);
        }

        if (state.PgPl && ControlFlowComponentHelpers.HasPgDeclareBlockOpener(state))
        {
            if (!(state.My || state.Ms) || state.StatementLabel?.Dialect == StatementLabelDialect.Pg)
            {
                return ControlFlowParser.ParsePgPlBeginEnd(state);
            }
            if (state.TryParse(ControlFlowParser.ParsePgPlBeginEnd, out BeginEnd? block))
            {
                return block;
            }
        }

        if (ControlFlowComponentHelpers.MaybeDeclareCursor(state))
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

    /// <summary>
    /// <code><![CDATA[
    /// CALL procedure_name ( [ argument [, ...] ] )
    /// ]]></code>
    /// </summary>
    protected virtual Call ParseCall(ParserState state)
    {
        state.ExpectKeyword(Keyword.CALL);
        ObjectName name = ComponentParser.ParseObjectName(state);
        state.ExpectLeftParen();
        return new Call(new FunctionCall(name) { Arguments = ExpressionParser.ParseFunctionArgumentList(state) });
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
        MetaData subStatementMeta = statement.Meta;
        statement = statementLabel.Dialect switch
        {
            StatementLabelDialect.My => new LabeledStatement.My(statement, statementLabel.Label),
            StatementLabelDialect.Pg => new LabeledStatement.Pg(statement, statementLabel.Label)
            {
                LabelMeta = new MetaData
                {
                    PreNonSql = statementLabel.PgMiddleNonSql?.Items,
                    PostNonSql = statementLabel.PgClosingNonSql?.Items
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        statement = statement with
        {
            Meta = new() { End = subStatementMeta.End, EndPlusTerminatorLength = subStatementMeta.EndPlusTerminatorLength }
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

    public virtual bool IsStatementTerminator(Token token, ParserState state)
    {
        return token is Semicolon or Eof;
    }
}
