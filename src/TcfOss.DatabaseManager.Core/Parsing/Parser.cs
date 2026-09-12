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
    public Func<string, bool> IsBuiltInFunction { get; protected init; }
    public Func<string, bool> AllowsFunctionCallWithoutParentheses { get; protected init; }

    public Parser()
    {
        PrecedenceManager = new PrecedenceManager();
        IsBuiltInFunction = new FunctionNameProvider().IsBuiltInFunction;
        AllowsFunctionCallWithoutParentheses = FunctionNameProvider.AllowsFunctionCallWithoutParentheses;
        ValueParser = new ValueParser();
        DataTypeParser = new DataTypeParser();
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
        Label? label = null;

        if (token is Label l)
        {
            state.Next();
            label = l;
            token = state.Peek();
        }

        Statement statement = ParseNormalStatement(state, token, label);

        if (token.PreNonSql.SafeAny())
        {
            statement.Meta.PreNonSql = token.PreNonSql.ToStatementNonSql();
            statement.Meta.Start = token.PreNonSql.First().Location.Position;
        }
        else
        {
            statement.Meta.Start = token.Location.Position;
        }


        Token upcoming = state.Peek();
        if (upcoming is Semicolon or Eof && upcoming.PreNonSql.SafeAny())
        {
            statement.Meta.PostNonSql = upcoming.PreNonSql.ToStatementNonSql();
        }

        int end = upcoming.Location.Position;
        int endPlusTerminatorLength = end;
        if (upcoming is Semicolon || upcoming is MyTerminator)
        {
            endPlusTerminatorLength += upcoming.Length;
        }
        statement.Meta.End = end;
        statement.Meta.EndPlusTerminatorLength = endPlusTerminatorLength;

        if (label != null)
        {
            statement = new LabeledStatement(statement, label.Identifier);
            if (label.PreNonSql.SafeAny())
            {
                statement.Meta.PreNonSql = label.PreNonSql.ToStatementNonSql();
                statement.Meta.Start = label.PreNonSql.First().Location.Position;
            }
            else
            {
                statement.Meta.Start = label.Location.Position;
            }
            statement.Meta.End = end;
        }

        return statement;
    }

    protected virtual Statement ParseNormalStatement(ParserState state, Token token, Label? label)
    {
        return token switch
        {
            Word word => ParseKeywordStatement(state, word, label),
            ParenOpen => SelectParser.ParseSelect(state, null),
            _ => throw ParserState.ExpectedException("SQL statement".Italic(), token)
        };
    }

    protected virtual Statement ParseKeywordStatement(ParserState state, Word first, Label? label)
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

    private Statement ParseCreate(ParserState state)
    {
        state.ExpectKeyword(Keyword.CREATE);

        CreateOrLabel? createOrLabel = ComponentParser.ParseCreateOrLabel(state);

        ViewAlgorithm? viewAlgorithm = DdlParser.ParseViewAlgorithm(state);
        Definer? definer = ComponentParser.ParseOptionalDefiner(state);
        SecurityContext? securityContext = DdlParser.ParseSecurityContext(state);
        bool aggregate = state.ParseKeyword(Keyword.AGGREGATE);
        bool temporary = state.ParseKeywordsAny(Keyword.TEMPORARY, Keyword.TEMP) != Keyword.undefined;

        return state.Peek() switch
        {
            Word { Keyword: Keyword.TABLE } => TableParser.ParseCreate(state, createOrLabel == CreateOrLabel.OrReplace, temporary),
            Word { Keyword: Keyword.TRIGGER } => DdlParser.ParseCreateTrigger(state, createOrLabel, definer),
            Word { Keyword: Keyword.FUNCTION } => DdlParser.ParseCreateFunction(state, aggregate, createOrLabel, definer),
            Word { Keyword: Keyword.PROCEDURE or Keyword.PROC } => DdlParser.ParseCreateProcedure(state, createOrLabel, definer),
            Word { Keyword: Keyword.VIEW } => DdlParser.ParseCreateView(state, createOrLabel, definer, viewAlgorithm, securityContext),
            Word { Keyword: Keyword.EVENT } => DdlParser.ParseCreateEvent(state, createOrLabel, definer),
            Word { Keyword: Keyword.INDEX or Keyword.UNIQUE or Keyword.FULLTEXT or Keyword.SPATIAL or Keyword.CLUSTERED or Keyword.NONCLUSTERED } => DdlParser.ParseCreateIndex(state, createOrLabel),
            _ => throw state.ExpectedException("TABLE", "TRIGGER", "VIEW", "FUNCTION", "PROCEDURE", "EVENT", "INDEX")
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
}
