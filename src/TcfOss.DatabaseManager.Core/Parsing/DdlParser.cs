using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;
using TriggerOrder = TcfOss.DatabaseManager.Core.Statements.Components.TriggerOrder;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class DdlParser
{
    protected virtual bool SupportsIndexMethodAfterTableName => true;
    protected virtual bool SupportsPostgresIndexSyntax => true;
    protected virtual bool SupportsIndexOptionsAfterFilter => true;
    protected virtual RoutineParamsSupportStyle GetRoutineParamsSupportStyle(bool isFunction) => RoutineParamsSupportStyle.KitchenSink;
    protected virtual bool SupportsOmittedFunctionReturn => true;
    protected virtual bool SupportsPostgresTriggerSyntax => true;
    protected virtual bool SupportsQualifiedTriggerNames => true;

    private readonly Parser _p;
    private readonly Func<ParserState, ObjectName> _parseObjectName;
    private readonly Func<ParserState, Identifier> _parseIdentifier;

    public DdlParser(Parser parser)
    {
        _p = parser;
        _parseObjectName = _p.ComponentParser.ParseObjectName;
        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
    }

    public CommentOn ParseCommentOn(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.COMMENT, Keyword.ON);
        PgStatementCommentTarget target;
        if (state.ParseKeyword(Keyword.COLUMN))
        {
            target = new PgStatementCommentTarget.Column(_parseObjectName(state));
        }
        else if (state.ParseKeyword(Keyword.TRIGGER))
        {
            Identifier name = _parseIdentifier(state);
            state.ExpectKeyword(Keyword.ON);
            target = new PgStatementCommentTarget.Trigger(name, _parseObjectName(state));
        }
        else
        {
            Keyword kind = state.ParseKeywordsAny(Keyword.TABLE, Keyword.VIEW, Keyword.INDEX,
                Keyword.TYPE, Keyword.DOMAIN, Keyword.SEQUENCE, Keyword.FUNCTION, Keyword.PROCEDURE);
            if (kind == Keyword.undefined)
            {
                throw state.ExpectedException("TABLE", "COLUMN", "VIEW", "INDEX", "TYPE", "DOMAIN", "SEQUENCE", "FUNCTION", "PROCEDURE", "TRIGGER");
            }
            PgCommentObjectType objectType = PgCommentObjectType.Parse(kind.ToString());
            ObjectName name = _parseObjectName(state);
            if (kind is Keyword.FUNCTION or Keyword.PROCEDURE)
            {
                SqlValueList<StatementRoutineParameter>? parameters = state.PeekIs<ParenOpen>()
                    ? ParseRoutineParameterList(state, kind == Keyword.FUNCTION) : null;
                if (parameters != null && parameters.Cast<StatementRoutineParameter.General>().Any(parameter => parameter.ParameterDefault != null
                    || parameter.Options != null))
                {
                    throw state.ExpectedException("routine signature without defaults or SQL Server options");
                }
                target = new PgStatementCommentTarget.Routine(objectType, name, parameters);
            }
            else
            {
                target = new PgStatementCommentTarget.DatabaseObject(objectType, name);
            }
        }
        state.ExpectKeyword(Keyword.IS);
        if (state.Peek() is not (StringLiteral or EscapeStringLiteral or UnicodeStringLiteral or DollarQuotedStringLiteral or Word { Keyword: Keyword.NULL }))
        {
            throw state.ExpectedException("string literal", "NULL");
        }
        return new CommentOn(target, ValueParser.ParseValue(state));
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// BEFORE | AFTER | INSTEAD OF | FOR
    /// ]]>
    /// </code>
    /// </summary>
    public virtual TriggerTime ParseTriggerTime(ParserState state)
    {
        if (state.ParseKeyword(Keyword.BEFORE))
        {
            return TriggerTime.Before;
        }
        if (state.ParseKeyword(Keyword.AFTER))
        {
            return TriggerTime.After;
        }
        if (state.ParseKeywordsAll(Keyword.INSTEAD, Keyword.OF))
        {
            return TriggerTime.InsteadOf;
        }
        if (state.ParseKeyword(Keyword.FOR))
        {
            return TriggerTime.For;
        }
        throw state.ExpectedException("BEFORE", "AFTER", "INSTEAD OF", "FOR");
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// INSERT
    ///   | UPDATE [ OF column_name [, ...] ]
    ///   | DELETE
    ///   | TRUNCATE
    /// ]]>
    /// </code>
    /// </summary>
    private TriggerEvent ParseTriggerEventBroad(ParserState state)
    {
        Keyword? next = (state.Next() as Word)?.Keyword;
        return next switch
        {
            Keyword.INSERT => new TriggerEvent.Insert(),
            Keyword.UPDATE => new TriggerEvent.Update(
                state.ParseKeyword(Keyword.OF)
                    ? state.ParseCommaSeparated(_parseIdentifier)
                    : null),
            Keyword.DELETE => new TriggerEvent.Delete(),
            Keyword.TRUNCATE => new TriggerEvent.Truncate(),
            _ => throw state.ExpectedException("INSERT", "UPDATE", "DELETE", "TRUNCATE")
        };
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// INSERT | UPDATE | DELETE
    /// ]]>
    /// </code>
    /// </summary>
    protected static TriggerEvent ParseTriggerEventNarrow(ParserState state)
    {
        Keyword? next = (state.Next() as Word)?.Keyword;
        return next switch
        {
            Keyword.INSERT => new TriggerEvent.Insert(),
            Keyword.UPDATE => new TriggerEvent.Update(null),
            Keyword.DELETE => new TriggerEvent.Delete(),
            _ => throw state.ExpectedException("INSERT", "UPDATE", "DELETE")
        };
    }

    /// <summary>
    /// See <see cref="ParseTriggerEventBroad"/>
    /// </summary>
    protected virtual TriggerEvent ParseTriggerEvent(ParserState state)
    {
        return ParseTriggerEventBroad(state);
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [ OR trigger_event ... ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual List<TriggerEvent>? ParseAdditionalTriggerEventsViaOr(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.OR))
        {
            return null;
        }
        List<TriggerEvent> events = [];
        do
        {
            events.Add(ParseTriggerEvent(state));
        } while (state.ParseKeyword(Keyword.OR));
        return events;
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [, trigger_event ...]
    /// ]]>
    /// </code>
    /// </summary>
    /// <remarks>
    /// See <see cref="ParseAdditionalTriggerEvents"/> for <c>trigger_event</c>.
    /// </remarks>
    protected virtual List<TriggerEvent>? ParseAdditionalTriggerEventsViaComma(ParserState state)
    {
        if (!state.PeekIs<Comma>())
        {
            return null;
        }
        List<TriggerEvent> events = [];
        while (state.ConsumeTokenIs<Comma>())
        {
            events.Add(ParseTriggerEvent(state));
        }
        return events;
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [ OR trigger_event ... ]
    ///   | [, trigger_event ...]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual List<TriggerEvent> ParseAdditionalTriggerEvents(ParserState state, out bool eventsUseOr)
    {
        List<TriggerEvent>? events = ParseAdditionalTriggerEventsViaOr(state);
        if (events != null)
        {
            eventsUseOr = true;
            return events;
        }
        events = ParseAdditionalTriggerEventsViaComma(state);
        if (events != null)
        {
            eventsUseOr = false;
            return events;
        }
        eventsUseOr = false;
        return [];
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// trigger_event [ [ OR trigger_event ... ] | [ , trigger_event ...] ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual SqlValueList<TriggerEvent> ParseTriggerEvents(ParserState state, out bool eventsUseOr)
    {
        return
        [
            ParseTriggerEvent(state),
            .. ParseAdditionalTriggerEvents(state, out eventsUseOr)
        ];
    }


    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [ WITH { ENCRYPTION | SCHEMABINDING } [ , ... ] ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual MsViewWithOptions? ParseOptionalViewWithOptions(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.WITH))
        {
            return null;
        }

        bool encryption = false;
        bool schemaBinding = false;

        while (true)
        {
            if (state.ParseKeyword(Keyword.ENCRYPTION))
            {
                if (encryption)
                {
                    throw new ParseException.Duplicate("ENCRYPTION", state.Peek().Location);
                }
                encryption = true;
            }
            else if (state.ParseKeyword(Keyword.SCHEMABINDING))
            {
                if (schemaBinding)
                {
                    throw new ParseException.Duplicate("SCHEMABINDING", state.Peek().Location);
                }
                schemaBinding = true;
            }
            else
            {
                throw state.ExpectedException("ENCRYPTION", "SCHEMABINDING");
            }

            if (!state.ConsumeTokenIs<Comma>())
            {
                break;
            }
        }

        return new MsViewWithOptions { Encryption = encryption, SchemaBinding = schemaBinding };
    }


    /// <summary>
    /// Parses a T-SQL <c>EXECUTE AS</c> target: <c>CALLER</c>, <c>SELF</c>,
    /// <c>OWNER</c>, or <c>'username'</c>.
    /// </summary>
    private static ExecuteAsClause ParseExecuteAsTarget(ParserState state)
    {
        if (state.ParseKeyword(Keyword.CALLER))
        {
            return new ExecuteAsClause.Caller();
        }
        if (state.ParseKeyword(Keyword.SELF))
        {
            return new ExecuteAsClause.Self();
        }
        if (state.ParseKeyword(Keyword.OWNER))
        {
            return new ExecuteAsClause.Owner();
        }
        if (state.Peek() is StringLiteral)
        {
            Value value = ValueParser.ParseValue(state);
            if (value is Value.SingleQuotedString sqs)
            {
                return new ExecuteAsClause.User(sqs);
            }
            throw new DefensiveGuardException("EXECUTE AS user value type", value.GetType());
        }
        throw state.ExpectedException("CALLER", "SELF", "OWNER", "'username'");
    }

    public virtual CreateTrigger ParseCreateTrigger(ParserState state, CreateOrLabel? createOrLabel = null, Definer? definer = null)
    {
        bool isConstraint = SupportsPostgresTriggerSyntax && state.ParseKeyword(Keyword.CONSTRAINT);
        state.ExpectKeyword(Keyword.TRIGGER);

        bool ifNotExists = ParseOptionalTriggerIfNotExists(state);

        ObjectName name = ParseTriggerName(state);

        ObjectName? tableName = ParseOptionalTriggerOnTableBeforeTime(state);
        bool tableNameBeforeTimeAndEvent = tableName != null;

        TriggerTime triggerTime = ParseTriggerTime(state);
        SqlValueList<TriggerEvent> events = ParseTriggerEvents(state, out bool eventsUseOr);

        if (!tableNameBeforeTimeAndEvent)
        {
            tableName = ParseTriggerOnTable(state);
        }

        PgTriggerConstraint? constraint = isConstraint ? ParseTriggerConstraint(state) : null;
        SqlValueList<PgTriggerTransitionRelation>? transitionRelations = ParseOptionalTriggerTransitionRelations(state);
        TriggerExecutionQuantifier? executionQuantifier = ParseOptionalTriggerExecutionQuantifier(state);
        Expression? when = ParseOptionalTriggerWhen(state);
        TriggerOrder? order = ParseOptionalTriggerOrder(state);

        bool triggerStartsWithAs = ParseTriggerBodyStartsWithAs(state);

        Statement body = ParseTriggerBody(state);

        return new CreateTrigger(name, triggerTime, events, tableName!, body)
        {
            Definer = definer,
            Order = order,
            CreateOrLabel = createOrLabel,
            IfNotExists = ifNotExists,
            TableNameBeforeTimeAndEvent = tableNameBeforeTimeAndEvent,
            ExecutionQuantifier = executionQuantifier,
            TriggerBodyStartsWithAs = triggerStartsWithAs,
            PgOptions = eventsUseOr || constraint != null || transitionRelations != null || when != null
                ? new PgStatementTriggerOptions
                {
                    EventsUseOr = eventsUseOr,
                    ConstraintOptions = constraint,
                    TransitionRelations = transitionRelations,
                    When = when
                } : null
        };
    }

    protected virtual bool ParseOptionalTriggerIfNotExists(ParserState state)
    {
        return state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);
    }

    protected virtual ObjectName ParseTriggerName(ParserState state)
    {
        return SupportsQualifiedTriggerNames ? _parseObjectName(state) : new ObjectName(_parseIdentifier(state));
    }

    private PgTriggerConstraint ParseTriggerConstraint(ParserState state)
    {
        ObjectName? fromTable = state.ParseKeyword(Keyword.FROM) ? _parseObjectName(state) : null;
        bool? deferrable = null;
        if (state.ParseKeyword(Keyword.DEFERRABLE))
        {
            deferrable = true;
        }
        else if (state.ParseKeywordsAll(Keyword.NOT, Keyword.DEFERRABLE))
        {
            deferrable = false;
        }

        bool? initiallyDeferred = null;
        if (state.ParseKeyword(Keyword.INITIALLY))
        {
            initiallyDeferred = state.ParseKeywordsAny(Keyword.DEFERRED, Keyword.IMMEDIATE) switch
            {
                Keyword.DEFERRED => true,
                Keyword.IMMEDIATE => false,
                _ => throw state.ExpectedException("DEFERRED", "IMMEDIATE")
            };
        }
        return new PgTriggerConstraint(fromTable, deferrable, initiallyDeferred);
    }

    protected virtual SqlValueList<PgTriggerTransitionRelation>? ParseOptionalTriggerTransitionRelations(ParserState state)
    {
        if (!SupportsPostgresTriggerSyntax || !state.ParseKeyword(Keyword.REFERENCING))
        {
            return null;
        }
        List<PgTriggerTransitionRelation> relations = [];
        do
        {
            bool isNew = state.ParseKeywordsAny(Keyword.OLD, Keyword.NEW) switch
            {
                Keyword.OLD => false,
                Keyword.NEW => true,
                _ => throw state.ExpectedException("OLD", "NEW")
            };
            state.ExpectKeyword(Keyword.TABLE);
            bool useAs = state.ParseKeyword(Keyword.AS);
            relations.Add(new PgTriggerTransitionRelation(isNew, _parseIdentifier(state), useAs));
        } while (state.PeekKeyword(Keyword.OLD) || state.PeekKeyword(Keyword.NEW));
        return [.. relations];
    }

    protected virtual Expression? ParseOptionalTriggerWhen(ParserState state)
    {
        if (!SupportsPostgresTriggerSyntax || !state.ParseKeyword(Keyword.WHEN))
        {
            return null;
        }
        return state.ParseParenthesized(_p.ExpressionParser.ParseExpr);
    }

    protected virtual Statement ParseTriggerBody(ParserState state)
    {
        if (SupportsPostgresTriggerSyntax && state.PeekKeyword(Keyword.EXECUTE)
            && state.PeekNth(1) is Word { Keyword: Keyword.FUNCTION or Keyword.PROCEDURE })
        {
            return ParseTriggerExecute(state);
        }
        return _p.ParseStatement(state);
    }

    protected PgTriggerExecute ParseTriggerExecute(ParserState state)
    {
        Token token = state.Peek();
        state.ExpectKeyword(Keyword.EXECUTE);
        bool useProcedure = state.ParseKeywordsAny(Keyword.FUNCTION, Keyword.PROCEDURE) switch
        {
            Keyword.FUNCTION => false,
            Keyword.PROCEDURE => true,
            _ => throw state.ExpectedException("FUNCTION", "PROCEDURE")
        };
        ObjectName functionName = _parseObjectName(state);
        SqlValueList<PgStatementTriggerArgument> arguments = state.ParseParenthesizedCommaSeparated(ParseTriggerArgument, allowEmpty: true);
        var statement = new PgTriggerExecute(functionName, arguments, useProcedure);
        Parser.AttachStatementMetadata(statement, token, state);
        return statement;
    }

    protected virtual PgStatementTriggerArgument ParseTriggerArgument(ParserState state)
    {
        if (state.Peek() is Word)
        {
            return new PgStatementTriggerArgument.Named(_parseIdentifier(state));
        }
        return new PgStatementTriggerArgument.Literal(ValueParser.ParseValue(state));
    }

    protected virtual ObjectName? ParseOptionalTriggerOnTableBeforeTime(ParserState state)
    {
        if (state.ParseKeyword(Keyword.ON))
        {
            return _parseObjectName(state);
        }
        return null;
    }

    private ObjectName ParseTriggerOnTable(ParserState state)
    {
        state.ExpectKeyword(Keyword.ON);
        return _parseObjectName(state);
    }

    protected virtual TriggerExecutionQuantifier.ForEach? ParseOptionalTriggerExecutionQuantifier(ParserState state)
    {
        if (!SupportsPostgresTriggerSyntax)
        {
            if (!state.ParseKeywordsAll(Keyword.FOR, Keyword.EACH))
            {
                return null;
            }
            return new TriggerExecutionQuantifier.ForEach(ParseTriggerForEachType(state));
        }
        if (!state.ParseKeyword(Keyword.FOR))
        {
            return null;
        }

        bool useEach = state.ParseKeyword(Keyword.EACH);
        TriggerForEachType type = ParseTriggerForEachType(state);
        return new TriggerExecutionQuantifier.ForEach(type) { UseEach = useEach };
    }

    private static TriggerForEachType ParseTriggerForEachType(ParserState state)
    {
        Keyword? next = (state.Next() as Word)?.Keyword;
        return next switch
        {
            Keyword.ROW => TriggerForEachType.Row,
            Keyword.STATEMENT => TriggerForEachType.Statement,
            _ => throw state.ExpectedException("ROW", "STATEMENT")
        };
    }

    protected virtual TriggerOrder? ParseOptionalTriggerOrder(ParserState state)
    {
        TriggerOrderPosition orderPosition;
        if (state.ParseKeyword(Keyword.FOLLOWS))
        {
            orderPosition = TriggerOrderPosition.Follows;
        }
        else if (state.ParseKeyword(Keyword.PRECEDES))
        {
            orderPosition = TriggerOrderPosition.Precedes;
        }
        else
        {
            return null;
        }
        return new TriggerOrder(orderPosition, _parseObjectName(state));
    }


    protected virtual bool ParseTriggerBodyStartsWithAs(ParserState state)
    {
        return state.ParseKeyword(Keyword.AS);
    }

    private static RoutineParameterDirection? ParseRoutineParameterDirection(ParserState state, RoutineParameterModeSupport supportedModes)
    {
        if (supportedModes.HasFlag(RoutineParameterModeSupport.InOut)
            && state.ParseKeywordsAll(Keyword.IN, Keyword.OUT))
        {
            return RoutineParameterDirection.InOut;
        }

        if (state.Peek() is Word { QuoteStyle: QuoteStyle.None, Sigil: SigilKind.None } word
            && RoutineParameterDirection.TryParse(word.Value, out RoutineParameterDirection? direction)
            && supportedModes.HasFlag(GetRoutineParameterModeSupport(direction)))
        {
            state.Next();
            return direction;
        }

        return null;
    }

    private static RoutineParameterModeSupport GetRoutineParameterModeSupport(RoutineParameterDirection direction)
    {
        if (direction == RoutineParameterDirection.In)
        {
            return RoutineParameterModeSupport.In;
        }
        if (direction == RoutineParameterDirection.Out)
        {
            return RoutineParameterModeSupport.Out;
        }
        if (direction == RoutineParameterDirection.InOut)
        {
            return RoutineParameterModeSupport.InOut;
        }
        return RoutineParameterModeSupport.Variadic;
    }

    public static SecurityContext? ParseSecurityContext(ParserState state)
    {
        SecurityContext? securityContext = null;

        if (state.ParseKeywordsAll(Keyword.SQL, Keyword.SECURITY))
        {
            securityContext = state.Peek() switch
            {
                Word { Keyword: Keyword.DEFINER } => SecurityContext.Definer,
                Word { Keyword: Keyword.INVOKER } => SecurityContext.Invoker,
                _ => throw state.ExpectedException<SecurityContext>()
            };
            state.Next();
        }

        return securityContext;
    }

    public static ViewAlgorithm? ParseViewAlgorithm(ParserState state)
    {
        ViewAlgorithm? algorithm = null;
        if (state.ParseKeyword(Keyword.ALGORITHM))
        {
            state.ExpectToken<Equal>();
            algorithm = state.Peek() switch
            {
                Word { Keyword: Keyword.UNDEFINED } => ViewAlgorithm.Undefined,
                Word { Keyword: Keyword.MERGE } => ViewAlgorithm.Merge,
                Word { Keyword: Keyword.TEMPTABLE } => ViewAlgorithm.TempTable,
                _ => throw state.ExpectedException<ViewAlgorithm>()
            };
            state.Next();
        }
        return algorithm;
    }

    private static ViewCheckOption? ParseViewCheckOption(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.WITH))
        {
            return null;
        }

        ViewCheckOptionScope? scope = state.MaybeParseStringEnum<ViewCheckOptionScope>();
        state.ExpectKeywordsAll(Keyword.CHECK, Keyword.OPTION);

        if (scope is not null)
        {
            return new ViewCheckOption.Specified(scope);
        }
        return new ViewCheckOption.Unspecified();
    }

    protected virtual SqlValueList<StatementRoutineParameter> ParseRoutineParameterList(ParserState state, bool isFunction)
    {
        RoutineParamsSupportStyle style = GetRoutineParamsSupportStyle(isFunction);
        return state.ParseParenthesizedCommaSeparated(innerState => ParseRoutineParameter(innerState, style), allowEmpty: true);
    }

    protected virtual StatementRoutineParameter ParseRoutineParameter(ParserState state, RoutineParamsSupportStyle style)
    {
        RoutineParameterDirection? direction = ParseRoutineParameterDirection(state, style.Modes);
        if (style.NameMode == RoutineParameterNameMode.RequiredVariable)
        {
            Identifier variableName = _parseIdentifier(state);
            if (variableName.Sigil != SigilKind.Variable)
            {
                throw state.ExpectedException("parameter name (@name)");
            }
            DataTypeReference variableType = _p.DataTypeParser.ParseDataTypeReference(state);
            (ParameterDefault? variableDefault, MsRoutineParameterOptions? options) = ParseRoutineParameterSuffix(state, style);
            var defaultValue = (ParameterDefault.ValueDefault?)variableDefault;
            return new StatementRoutineParameter.Undirected(variableName, variableType, defaultValue)
            {
                Options = options
            };
        }

        if (style.NameMode == RoutineParameterNameMode.RequiredIdentifier)
        {
            Identifier requiredName = _parseIdentifier(state);
            DataTypeReference requiredType = _p.DataTypeParser.ParseDataTypeReference(state);
            return new StatementRoutineParameter.Directed(requiredName, requiredType, direction,
                ParseRoutineParameterDefault(state, style));
        }

        if (CanStartUnnamedRoutineParameter(state)
            && state.TryParse(innerState => ParseUnnamedRoutineParameter(innerState, direction, style), out StatementRoutineParameter.General? unnamed))
        {
            return unnamed;
        }

        Identifier name = _parseIdentifier(state);
        RoutineParameterDirection? namedDirection = direction ?? ParseRoutineParameterDirection(state, style.Modes);
        DataTypeReference namedType = _p.DataTypeParser.ParseDataTypeReference(state);
        (ParameterDefault? namedDefault, MsRoutineParameterOptions? namedOptions) = ParseRoutineParameterSuffix(state, style);
        return new StatementRoutineParameter.General(name, namedType, namedDirection, namedDefault, namedOptions);
    }

    private bool CanStartUnnamedRoutineParameter(ParserState state)
    {
        return _p.DataTypeParser.IsDataTypeStart(state.Peek()) || state.PeekNth(1) switch
        {
            Comma or ParenClose or Dot or BracketOpen or Equal => true,
            Word { Keyword: Keyword.DEFAULT or Keyword.ARRAY } => true,
            _ => false
        };
    }

    private StatementRoutineParameter.General ParseUnnamedRoutineParameter(ParserState state, RoutineParameterDirection? direction,
        RoutineParamsSupportStyle style)
    {
        DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReference(state);
        (ParameterDefault? defaultValue, MsRoutineParameterOptions? options) = ParseRoutineParameterSuffix(state, style);
        if (!state.PeekIs<Comma>() && !state.PeekIs<ParenClose>())
        {
            throw state.ExpectedException(",", ")");
        }
        return new StatementRoutineParameter.General(null, dataType, direction, defaultValue, options);
    }

    private (ParameterDefault? Default, MsRoutineParameterOptions? Options) ParseRoutineParameterSuffix(ParserState state,
        RoutineParamsSupportStyle style)
    {
        MsRoutineParameterOptions? optionsBeforeDefault = style.AllowSqlServerOptions
            ? ParseSqlServerParameterOptionsBeforeDefault(state)
            : null;

        ParameterDefault? defaultValue = ParseRoutineParameterDefault(state, style);

        MsRoutineParameterOptions? options = style.AllowSqlServerOptions
            ? ParseSqlServerParameterOptionsAfterDefault(state, optionsBeforeDefault)
            : optionsBeforeDefault;
        return (defaultValue, options);
    }

    private ParameterDefault? ParseRoutineParameterDefault(ParserState state, RoutineParamsSupportStyle style)
    {
        bool useDefaultKeyword;
        if (style.DefaultSyntax.HasFlag(RoutineParameterDefaultSyntax.DefaultKeyword) && state.ParseKeyword(Keyword.DEFAULT))
        {
            useDefaultKeyword = true;
        }
        else if (style.DefaultSyntax.HasFlag(RoutineParameterDefaultSyntax.Equals) && state.ConsumeTokenIs<Equal>())
        {
            useDefaultKeyword = false;
        }
        else
        {
            return null;
        }

        if (style.DefaultKinds.HasFlag(RoutineParameterDefaultKind.Expression))
        {
            return new ParameterDefault.ExpressionDefault(_p.ExpressionParser.ParseExpr(state), useDefaultKeyword);
        }
        return new ParameterDefault.ValueDefault(ValueParser.ParseValue(state), useDefaultKeyword);
    }

    private static MsRoutineParameterOptions? ParseSqlServerParameterOptionsBeforeDefault(ParserState state)
    {
        bool varying = state.ParseKeyword(Keyword.VARYING);
        bool? nullable = null;
        if (state.ParseKeyword(Keyword.NULL))
        {
            nullable = true;
        }
        else if (state.ParseKeywordsAll(Keyword.NOT, Keyword.NULL))
        {
            nullable = false;
        }
        return varying || nullable.HasValue
            ? new MsRoutineParameterOptions(Varying: varying, Nullable: nullable)
            : null;
    }

    private static MsRoutineParameterOptions? ParseSqlServerParameterOptionsAfterDefault(ParserState state, MsRoutineParameterOptions? options)
    {
        OutputKeyword? output = null;
        if (state.ParseKeyword(Keyword.OUTPUT))
        {
            output = OutputKeyword.Output;
        }
        else if (state.ParseKeyword(Keyword.OUT))
        {
            output = OutputKeyword.Out;
        }

        bool readOnly = state.ParseKeyword(Keyword.READONLY);
        if (output == null && !readOnly)
        {
            return options;
        }
        return options == null
            ? new MsRoutineParameterOptions(Output: output, ReadOnly: readOnly)
            : options with { Output = output, ReadOnly = readOnly };
    }

    protected virtual MyRoutineCharacteristic? ParseOptionalRoutineCharacteristic(ParserState state)
    {
        Comment? comment = null;
        bool? deterministic = null;
        string? language = null;
        SqlDataRelation? relation = null;
        SecurityContext? securityContext = null;

        // ReSharper disable once TooWideLocalVariableScope
        SecurityContext? tempSecurityContext;
        // ReSharper disable once TooWideLocalVariableScope
        Comment? tempComment;

        while (true)
        {
            if (state.ParseKeywordsAll(Keyword.NOT, Keyword.DETERMINISTIC))
            {
                deterministic = false;
            }
            else if (state.ParseKeyword(Keyword.DETERMINISTIC))
            {
                deterministic = true;
            }
            else if (state.ParseKeyword(Keyword.LANGUAGE))
            {
                language = state.Next() switch
                {
                    Word { Keyword: Keyword.SQL } => "SQL",
                    _ => throw state.ExpectedException("SQL")
                };
            }
            else if ((tempComment = ComponentParser.ParseComment(state)) != null)
            {
                comment = tempComment;
            }
            else if (state.ParseKeywordsAll(Keyword.CONTAINS, Keyword.SQL))
            {
                relation = SqlDataRelation.ContainsSql;
            }
            else if (state.ParseKeywordsAll(Keyword.NO, Keyword.SQL))
            {
                relation = SqlDataRelation.NoSql;
            }
            else if (state.ParseKeywordsAll(Keyword.READS, Keyword.SQL, Keyword.DATA))
            {
                relation = SqlDataRelation.ReadsSqlData;
            }
            else if (state.ParseKeywordsAll(Keyword.MODIFIES, Keyword.SQL, Keyword.DATA))
            {
                relation = SqlDataRelation.ModifiesSqlData;
            }
            else if ((tempSecurityContext = ParseSecurityContext(state)) != null)
            {
                securityContext = tempSecurityContext;
            }
            else
            {
                break;
            }
        }
        if (comment == null && language == null && deterministic == null && relation == null && securityContext == null)
        {
            return null;
        }

        return new MyRoutineCharacteristic()
        {
            Comment = comment,
            Language = language,
            Deterministic = deterministic,
            Relation = relation,
            SecurityContext = securityContext,
        };
    }

    /// <summary>
    /// Parses an optional T-SQL <c>WITH</c> options block for routines
    /// (<c>ENCRYPTION</c>, <c>RECOMPILE</c>, <c>EXECUTE AS ...</c>). Returns
    /// <c>null</c> when no <c>WITH</c> is present.
    /// </summary>
    protected virtual MsRoutineWithOptions? ParseOptionalRoutineWithOptions(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.WITH))
        {
            return null;
        }

        bool withEncryption = false;
        bool withRecompile = false;
        ExecuteAsClause? executeAs = null;

        while (true)
        {
            if (state.ParseKeyword(Keyword.ENCRYPTION))
            {
                if (withEncryption)
                {
                    throw new ParseException.Duplicate("ENCRYPTION", state.Peek().Location);
                }
                withEncryption = true;
            }
            else if (state.ParseKeyword(Keyword.RECOMPILE))
            {
                if (withRecompile)
                {
                    throw new ParseException.Duplicate("RECOMPILE", state.Peek().Location);
                }
                withRecompile = true;
            }
            else if (state.ParseKeywordsAll(Keyword.EXECUTE, Keyword.AS))
            {
                if (executeAs is not null)
                {
                    throw new ParseException.Duplicate("EXECUTE AS", state.Peek().Location);
                }
                executeAs = ParseExecuteAsTarget(state);
            }
            else
            {
                throw state.ExpectedException("ENCRYPTION", "RECOMPILE", "EXECUTE AS");
            }

            if (!state.ConsumeTokenIs<Comma>())
            {
                break;
            }
        }

        return new MsRoutineWithOptions
        {
            Encryption = withEncryption,
            Recompile = withRecompile,
            ExecuteAs = executeAs
        };
    }

    public virtual CreateFunction ParseCreateFunction(ParserState state, bool aggregate = false, CreateOrLabel? createOrLabel = null, Definer? definer = null)
    {
        state.ExpectKeyword(Keyword.FUNCTION);

        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        ObjectName name = _parseObjectName(state);
        SqlValueList<StatementRoutineParameter> parameters = ParseRoutineParameterList(state, isFunction: true);

        StatementFunctionReturn? returnType = null;
        if (!state.PeekKeywordsAll(Keyword.RETURNS, Keyword.NULL, Keyword.ON, Keyword.NULL, Keyword.INPUT)
            && state.ParseKeyword(Keyword.RETURNS))
        {
            returnType = ParseFunctionReturn(state);
        }
        else if (!SupportsOmittedFunctionReturn)
        {
            throw state.ExpectedException("RETURNS");
        }
        ParsedRoutineTail tail = ParseRoutineTail(state, isFunction: true);
        if ((tail.PgOptionsBeforeBody?.Any(option => option is PgStatementRoutineOption.Rows) == true
                || tail.PgOptionsAfterBody?.Any(option => option is PgStatementRoutineOption.Rows) == true)
            && returnType is not (StatementFunctionReturn.SetOf or StatementFunctionReturn.Table))
        {
            throw state.ExpectedException("set-returning function for ROWS");
        }

        return new CreateFunction(name, parameters, returnType, tail.Body)
        {
            Definer = definer,
            CreateOrLabel = createOrLabel,
            Aggregate = aggregate,
            MyCharacteristic = tail.MyCharacteristic,
            MsOptions = tail.MsOptions,
            PgOptionsBeforeBody = tail.PgOptionsBeforeBody,
            PgOptionsAfterBody = tail.PgOptionsAfterBody,
            RoutineTailMeta = tail.Meta,
            UseAsBeforeBody = tail.UseAsBeforeBody,
            IfNotExists = ifNotExists
        };
    }

    protected virtual StatementFunctionReturn ParseFunctionReturn(ParserState state)
    {
        if (state.ParseKeyword(Keyword.TABLE))
        {
            return new StatementFunctionReturn.Table(state.ParseParenthesizedCommaSeparated(ParseFunctionReturnColumn, allowEmpty: false));
        }
        if (state.ParseKeyword(Keyword.SETOF))
        {
            StatementFunctionReturn.Element element = ParseFunctionReturnElement(state);
            return new StatementFunctionReturn.SetOf(element);
        }
        return ParseFunctionReturnElement(state);
    }

    protected StatementFunctionReturn.DataTypeReturn ParseScalarFunctionReturn(ParserState state)
    {
        return new StatementFunctionReturn.DataTypeReturn(_p.DataTypeParser.ParseDataTypeReference(state));
    }

    private StatementFunctionReturn.Element ParseFunctionReturnElement(ParserState state)
    {
        if (state.Peek() is Word { QuoteStyle: QuoteStyle.None, Sigil: SigilKind.None } word && !state.PeekNthIs<Dot>(1))
        {
            if (PgPseudoType.TryParse(word.Value, out PgPseudoType? pseudoType))
            {
                state.Next();
                return new StatementFunctionReturn.PseudoType(pseudoType);
            }
        }
        return ParseScalarFunctionReturn(state);
    }

    private StatementFunctionReturn.TableColumn ParseFunctionReturnColumn(ParserState state)
    {
        Identifier name = _parseIdentifier(state);
        DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReference(state);
        return new StatementFunctionReturn.TableColumn(name, dataType);
    }

    public virtual CreateProcedure ParseCreateProcedure(ParserState state, CreateOrLabel? createOrLabel = null, Definer? definer = null)
    {
        // Accept both PROCEDURE and the T-SQL abbreviation PROC.
        if (state.ParseKeywordsAny(Keyword.PROCEDURE, Keyword.PROC) == Keyword.undefined)
        {
            throw state.ExpectedException("PROCEDURE", "PROC");
        }
        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        ObjectName name = _parseObjectName(state);
        SqlValueList<StatementRoutineParameter> parameters = ParseRoutineParameterList(state, isFunction: false);

        ParsedRoutineTail tail = ParseRoutineTail(state, isFunction: false);

        return new CreateProcedure(name, parameters, tail.Body)
        {
            Definer = definer,
            CreateOrLabel = createOrLabel,
            MyCharacteristic = tail.MyCharacteristic,
            MsOptions = tail.MsOptions,
            PgOptionsBeforeBody = tail.PgOptionsBeforeBody,
            PgOptionsAfterBody = tail.PgOptionsAfterBody,
            RoutineTailMeta = tail.Meta,
            UseAsBeforeBody = tail.UseAsBeforeBody,
            IfNotExists = ifNotExists
        };
    }

    protected record ParsedRoutineTail(Statement Body, bool UseAsBeforeBody)
    {
        public MyRoutineCharacteristic? MyCharacteristic { get; init; }
        public MsRoutineWithOptions? MsOptions { get; init; }
        public SqlValueList<PgStatementRoutineOption>? PgOptionsBeforeBody { get; init; }
        public SqlValueList<PgStatementRoutineOption>? PgOptionsAfterBody { get; init; }
        public MetaData Meta { get; init; } = new();
    }

    protected virtual bool UseSqlRoutineTail(ParserState state)
    {
        if (state.PeekKeyword(Keyword.LANGUAGE))
        {
            return state.PeekNth(2) is Word
            {
                Keyword: Keyword.AS or Keyword.RETURN
                or Keyword.IMMUTABLE or Keyword.STABLE or Keyword.VOLATILE or Keyword.STRICT
                or Keyword.CALLED or Keyword.SECURITY or Keyword.EXTERNAL or Keyword.PARALLEL or Keyword.COST
                or Keyword.ROWS or Keyword.SET or Keyword.RETURNS
            }
                || (state.PeekNth(2) is Word { Keyword: Keyword.BEGIN }
                    && state.PeekNth(3) is Word { Keyword: Keyword.ATOMIC });
        }
        return state.PeekKeywordsAll(Keyword.BEGIN, Keyword.ATOMIC)
            || (state.PeekKeyword(Keyword.AS) && state.PeekNth(1) is StringLiteral or EscapeStringLiteral or UnicodeStringLiteral or DollarQuotedStringLiteral)
            || state.Peek() is Word
            {
                Keyword: Keyword.IMMUTABLE or Keyword.STABLE
                or Keyword.VOLATILE or Keyword.STRICT or Keyword.CALLED or Keyword.SECURITY
                or Keyword.EXTERNAL or Keyword.PARALLEL or Keyword.COST or Keyword.ROWS or Keyword.SET
            }
            || state.PeekKeywordsAll(Keyword.RETURNS, Keyword.NULL, Keyword.ON, Keyword.NULL, Keyword.INPUT);
    }

    protected virtual ParsedRoutineTail ParseRoutineTail(ParserState state, bool isFunction)
    {
        int start = state.Peek().Location.Position;
        if (UseSqlRoutineTail(state))
        {
            return ParseSqlRoutineTail(state, isFunction);
        }

        MyRoutineCharacteristic? characteristic = ParseOptionalRoutineCharacteristic(state);
        MsRoutineWithOptions? msOptions = ParseOptionalRoutineWithOptions(state);
        if (isFunction && msOptions is { Recompile: true })
        {
            throw state.ExpectedException("ENCRYPTION", "EXECUTE AS", "AS");
        }
        bool useAsBeforeBody = state.ParseKeyword(Keyword.AS);
        Statement body = _p.ParseStatement(state);
        return new ParsedRoutineTail(body, useAsBeforeBody)
        {
            MyCharacteristic = characteristic,
            MsOptions = msOptions,
            Meta = new MetaData { Start = start, End = state.Peek().Location.Position }
        };
    }

    public AlterRoutine ParseAlterRoutine(ParserState state)
    {
        PgRoutineKind kind = state.ParseStringEnum<PgRoutineKind>();
        PgRoutineReference routine = ParseRoutineReference(state);
        PgAlterRelationOperation? standalone = null;
        if (state.ParseKeyword(Keyword.RENAME))
        {
            state.ExpectKeyword(Keyword.TO);
            standalone = new PgAlterRelationOperation.RenameTo(_parseIdentifier(state));
        }
        else if (state.ParseKeyword(Keyword.OWNER))
        {
            state.ExpectKeyword(Keyword.TO);
            standalone = new PgAlterRelationOperation.OwnerTo(ParsePostgresOwner(state));
        }
        else if (state.ParseKeywordsAll(Keyword.SET, Keyword.SCHEMA))
        {
            standalone = new PgAlterRelationOperation.SetSchema(_parseIdentifier(state));
        }
        else if (state.Peek() is Word { Keyword: Keyword.NO or Keyword.DEPENDS })
        {
            bool no = state.ParseKeyword(Keyword.NO);
            state.ExpectKeywordsAll(Keyword.DEPENDS, Keyword.ON, Keyword.EXTENSION);
            standalone = new PgAlterRelationOperation.DependsOnExtension(_parseIdentifier(state)) { No = no };
        }
        if (standalone != null)
        {
            return new AlterRoutine(kind, routine, [new PgAlterRoutineOperation.Relation(standalone)]);
        }

        SqlValueList<PgAlterRoutineOperation> operations = [];
        while (true)
        {
            PgAlterRoutineOperation? operation = ParseNextAlterRoutineOperation(state, kind == PgRoutineKind.Function);
            if (operation == null)
            {
                break;
            }
            operations.Add(operation);
        }
        if (operations.Count == 0)
        {
            throw state.ExpectedException("ALTER routine action");
        }
        return new AlterRoutine(kind, routine, operations) { Restrict = state.ParseKeyword(Keyword.RESTRICT) };
    }

    protected virtual PgAlterRoutineOperation? ParseNextAlterRoutineOperation(ParserState state, bool isFunction)
    {
        if (state.ParseKeyword(Keyword.RESET))
        {
            return state.ParseKeyword(Keyword.ALL)
                ? new PgAlterRoutineOperation.ResetAll()
                : new PgAlterRoutineOperation.Reset(_parseObjectName(state));
        }
        if (state.ParseKeyword(Keyword.SET))
        {
            return new PgAlterRoutineOperation.SetOption(ParseStatementRoutineSetting(state, true));
        }
        if (state.PeekKeyword(Keyword.LANGUAGE))
        {
            return null;
        }
        PgStatementRoutineOption? option = ParseNextRoutineOption(state, isFunction);
        return option == null ? null : new PgAlterRoutineOperation.SetOption(option);
    }

    private PgStatementRoutineOption.RoutineSetting ParseStatementRoutineSetting(ParserState state, bool allowToDefault)
    {
        ObjectName name = _parseObjectName(state);
        if (state.ParseKeywordsAll(Keyword.FROM, Keyword.CURRENT))
        {
            return new PgStatementRoutineOption.RoutineSetting(new PgRoutineSetting.FromCurrent(name));
        }

        SettingAssignmentOperator op;
        if (state.ConsumeTokenIs<Equal>())
        {
            op = SettingAssignmentOperator.EqualTo;
        }
        else if (state.ParseKeyword(Keyword.TO))
        {
            op = SettingAssignmentOperator.To;
        }
        else
        {
            throw state.ExpectedException("=", "TO");
        }

        if (allowToDefault && state.ParseKeyword(Keyword.DEFAULT))
        {
            return new PgStatementRoutineOption.RoutineSetting(new PgRoutineSetting.ToDefault(name) { Op = op });
        }

        SqlValueList<Value> values = state.ParseCommaSeparated(ComponentParser.ParseSimpleValue);
        return new PgStatementRoutineOption.RoutineSetting(new PgRoutineSetting.ToValues(name, values) { Op = op });
    }

    private void ParseRoutineOptions(ParserState state, SqlValueList<PgStatementRoutineOption> options, HashSet<string> seenOptions, bool isFunction)
    {
        while (true)
        {
            Location location = state.Peek().Location;
            PgStatementRoutineOption? option = ParseNextRoutineOption(state, isFunction);
            if (option == null)
            {
                return;
            }
            if (!seenOptions.Add(option.UniqueName))
            {
                throw new ParseException.Duplicate(option.UniqueName, location);
            }
            options.Add(option);
        }
    }

    protected virtual PgStatementRoutineOption? ParseNextRoutineOption(ParserState state, bool isFunction)
    {
        if (state.ParseKeyword(Keyword.LANGUAGE))
        {
            return new PgStatementRoutineOption.Language(_parseIdentifier(state));
        }

        if (state.ParseKeyword(Keyword.SET))
        {
            return ParseStatementRoutineSetting(state, false);
        }

        List<Keyword> securityMatch = state.ParseKeywordSequence(KeywordSearchCondition.Optional(Keyword.EXTERNAL), Keyword.SECURITY);
        if (securityMatch.Count > 0)
        {
            bool external = securityMatch[0] == Keyword.EXTERNAL;
            SecurityContext context;
            if (state.ParseKeyword(Keyword.DEFINER))
            {
                context = SecurityContext.Definer;
            }
            else if (state.ParseKeyword(Keyword.INVOKER))
            {
                context = SecurityContext.Invoker;
            }
            else
            {
                throw state.ExpectedException("DEFINER", "INVOKER");
            }
            return new PgStatementRoutineOption.Security(context) { External = external };
        }

        if (!isFunction)
        {
            return null;
        }

        if (state.ParseKeyword(Keyword.PARALLEL))
        {
            PgRoutineParallel parallelMode;
            if (state.ParseKeyword(Keyword.UNSAFE))
            {
                parallelMode = PgRoutineParallel.Unsafe;
            }
            else if (state.ParseKeyword(Keyword.RESTRICTED))
            {
                parallelMode = PgRoutineParallel.Restricted;
            }
            else if (state.ParseKeyword(Keyword.SAFE))
            {
                parallelMode = PgRoutineParallel.Safe;
            }
            else
            {
                throw state.ExpectedException("UNSAFE", "RESTRICTED", "SAFE");
            }
            return new PgStatementRoutineOption.Parallel(parallelMode);
        }

        if (state.ParseKeyword(Keyword.STRICT))
        {
            return new PgStatementRoutineOption.NullHandling(PgRoutineNullHandling.Strict);
        }
        if (state.ParseKeywordsAll(Keyword.CALLED, Keyword.ON, Keyword.NULL, Keyword.INPUT))
        {
            return new PgStatementRoutineOption.NullHandling(PgRoutineNullHandling.CalledOnNullInput);
        }
        if (state.ParseKeywordsAll(Keyword.RETURNS, Keyword.NULL, Keyword.ON, Keyword.NULL, Keyword.INPUT))
        {
            return new PgStatementRoutineOption.NullHandling(PgRoutineNullHandling.ReturnsNullOnNullInput);
        }

        if (state.ParseKeyword(Keyword.COST))
        {
            return new PgStatementRoutineOption.Cost(ParsePositiveRoutineNumber(state));
        }

        if (state.ParseKeyword(Keyword.ROWS))
        {
            return new PgStatementRoutineOption.Rows(ParsePositiveRoutineNumber(state));
        }

        PgRoutineVolatility? volatility = state.MaybeParseStringEnum<PgRoutineVolatility>();
        if (volatility is not null)
        {
            return new PgStatementRoutineOption.Volatility(volatility);
        }

        return null;
    }

    private static Value.Number ParsePositiveRoutineNumber(ParserState state)
    {
        state.ConsumeTokenIs<Plus>();
        Token token = state.Next();
        if (token is NumericLiteral { IsLong: false } number
            && double.TryParse(number.GetDecimalText(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value)
            && double.IsFinite(value) && value > 0)
        {
            return new Value.Number(number.Value, false);
        }
        throw ParserState.ExpectedException("positive number", token);
    }


    protected ParsedRoutineTail ParseSqlRoutineTail(ParserState state, bool isFunction)
    {
        int start = state.Peek().Location.Position;
        SqlValueList<PgStatementRoutineOption> beforeBody = [];
        SqlValueList<PgStatementRoutineOption> afterBody = [];
        var seenOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ParseRoutineOptions(state, beforeBody, seenOptions, isFunction);
        bool useAsBeforeBody = state.ParseKeyword(Keyword.AS);
        Statement body;
        if (useAsBeforeBody)
        {
            if (state.Peek() is not (StringLiteral or EscapeStringLiteral or UnicodeStringLiteral or DollarQuotedStringLiteral))
            {
                throw state.ExpectedException("string literal");
            }
            using (state.EnterContext(ParseContext.StringLiteralAsStatement))
            {
                body = _p.ParseStatement(state);
            }
        }
        else if (state.PeekKeywordsAll(Keyword.BEGIN, Keyword.ATOMIC))
        {
            using (state.EnterContext(ParseContext.BeginAtomic | ParseContext.NarrowSqlOnly))
            {
                body = _p.ParseStatement(state);
            }
        }
        else if (isFunction && state.PeekKeyword(Keyword.RETURN))
        {
            using (state.EnterContext(ParseContext.NarrowSqlOnly))
            {
                body = _p.ParseStatement(state);
            }
        }
        else
        {
            throw state.ExpectedException(isFunction ? ["AS string literal", "BEGIN ATOMIC", "RETURN"] : ["AS string literal", "BEGIN ATOMIC"]);
        }

        ParseRoutineOptions(state, afterBody, seenOptions, isFunction);
        Identifier? language = beforeBody.Concat(afterBody).OfType<PgStatementRoutineOption.Language>().SingleOrDefault()?.Name;
        if (body is RoutineBodyLiteral)
        {
            if (language == null)
            {
                throw state.ExpectedException("LANGUAGE");
            }
        }
        else if (language != null && language.Name != "sql"
            && (language.QuoteStyle != QuoteStyle.None
                || !string.Equals(language.Name, "sql", StringComparison.OrdinalIgnoreCase)))
        {
            throw state.ExpectedException("LANGUAGE SQL");
        }

        return new ParsedRoutineTail(body, useAsBeforeBody)
        {
            PgOptionsBeforeBody = beforeBody,
            PgOptionsAfterBody = afterBody,
            Meta = new MetaData { Start = start, End = state.Peek().Location.Position }
        };
    }

    public CreateSequence ParseCreateSequence(ParserState state, TemporaryLabel? temporaryLabel, bool unlogged)
    {
        state.ExpectKeyword(Keyword.SEQUENCE);
        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);
        ObjectName name = _parseObjectName(state);
        return new CreateSequence(name, ParseSequenceOptions(state))
        {
            IfNotExists = ifNotExists,
            TemporaryLabel = temporaryLabel,
            Unlogged = unlogged
        };
    }

    public AlterSequence ParseAlterSequence(ParserState state)
    {
        state.ExpectKeyword(Keyword.SEQUENCE);
        bool ifExists = state.ParseKeywordsAll(Keyword.IF, Keyword.EXISTS);
        ObjectName name = _parseObjectName(state);
        PgAlterSequenceOperation operation;
        if (state.ParseKeywordsAll(Keyword.OWNER, Keyword.TO))
        {
            operation = new PgAlterSequenceOperation.OwnerTo(ParsePostgresOwner(state));
        }
        else if (state.ParseKeywordsAll(Keyword.RENAME, Keyword.TO))
        {
            operation = new PgAlterSequenceOperation.RenameTo(_parseIdentifier(state));
        }
        else if (state.ParseKeyword(Keyword.SET))
        {
            if (state.ParseKeyword(Keyword.SCHEMA))
            {
                operation = new PgAlterSequenceOperation.SetSchema(_parseIdentifier(state));
            }
            else
            {
                Keyword persistence = state.ParseKeywordsAny(Keyword.LOGGED, Keyword.UNLOGGED);
                if (persistence == Keyword.undefined)
                {
                    throw state.ExpectedException("SCHEMA", "LOGGED", "UNLOGGED");
                }
                operation = new PgAlterSequenceOperation.SetPersistence(persistence == Keyword.UNLOGGED);
            }
        }
        else
        {
            SqlValueList<PgStatementSequenceOption> options = ParseSequenceOptions(state, true);
            if (options.Count == 0)
            {
                throw state.ExpectedCategoryException("sequence alteration");
            }
            operation = new PgAlterSequenceOperation.Parameters(options);
        }
        return new AlterSequence(name, operation) { IfExists = ifExists };
    }

    private SqlValueList<PgStatementSequenceOption> ParseSequenceOptions(ParserState state, bool alter = false)
    {
        SqlValueList<PgStatementSequenceOption> options = [];
        var used = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            Location location = state.Peek().Location;
            PgStatementSequenceOption? option = ParseNextSequenceOption(state, alter);
            if (option == null)
            {
                return options;
            }
            if (!used.Add(option.UniqueName))
            {
                throw new ParseException.Duplicate(option.UniqueName, location);
            }
            options.Add(option);
        }
    }

    protected virtual PgStatementSequenceOption? ParseNextSequenceOption(ParserState state, bool alter)
    {
        if (!alter && state.PeekKeyword(Keyword.RESTART))
        {
            return null;
        }
        if (state.ParseKeyword(Keyword.AS))
        {
            return new PgStatementSequenceOption.DataType(state.ParseStringEnum<PgSequenceDataType>());
        }
        if (state.ParseKeyword(Keyword.OWNED))
        {
            state.ExpectKeyword(Keyword.BY);
            ObjectName? column = state.ParseKeyword(Keyword.NONE) ? null : _parseObjectName(state);
            if (column is { Values.Count: < 2 or > 4 })
            {
                throw state.ExpectedCategoryException("qualified column name");
            }
            return new PgStatementSequenceOption.OwnedBy(column);
        }
        return _p.ComponentParser.ParseSequenceOption(state);
    }

    public virtual CreateView ParseCreateView(ParserState state, CreateOrLabel? createOrLabel, Definer? definer, ViewAlgorithm? viewAlgorithm, SecurityContext? securityContext, TemporaryLabel? temporaryLabel, bool recursive)
    {
        state.ExpectKeyword(Keyword.VIEW);
        bool ifNotExists = ParseOptionalViewIfNotExists(state);

        ObjectName name = _parseObjectName(state);
        if (recursive && !state.PeekIs<ParenOpen>())
        {
            throw state.ExpectedException("(");
        }
        SqlValueList<Identifier>? columns = state.ParseParenthesizedCommaSeparatedOptional(_parseIdentifier, false);

        SqlValueList<PgStorageParameter>? pgOptions = ParseOptionalPgViewOptions(state);
        MsViewWithOptions? msOptions = ParseOptionalViewWithOptions(state);

        state.ExpectKeyword(Keyword.AS);

        Select body = ParseViewQuery(state);

        ViewCheckOption? viewCheckOption = null;
        if (!recursive)
        {
            viewCheckOption = ParseViewCheckOption(state);
        }

        return new CreateView(name, body)
        {
            Columns = columns,
            CreateOrLabel = createOrLabel,
            IfNotExists = ifNotExists,
            Definer = definer,
            ViewAlgorithm = viewAlgorithm,
            SecurityContext = securityContext,
            TemporaryLabel = temporaryLabel,
            Recursive = recursive,
            ViewCheckOption = viewCheckOption,
            MsOptions = msOptions,
            PgOptions = pgOptions,
        };
    }

    protected virtual SqlValueList<PgStorageParameter>? ParseOptionalPgViewOptions(ParserState state)
    {
        if (!state.PeekKeyword(Keyword.WITH) || !state.PeekNthIs<ParenOpen>(1))
        {
            return null;
        }
        state.ExpectKeyword(Keyword.WITH);
        return state.ParseParenthesizedCommaSeparated(_p.ComponentParser.ParseStorageParameter, false);
    }

    protected virtual bool ParseOptionalViewIfNotExists(ParserState state)
    {
        return state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);
    }

    private Select ParseViewQuery(ParserState state)
    {
        Token start = state.Peek();
        if (start is not (Word { Keyword: Keyword.SELECT or Keyword.VALUES or Keyword.WITH } or ParenOpen))
        {
            throw state.ExpectedException("SELECT", "VALUES", "WITH", "subquery");
        }
        if (_p.ParseStatement(state) is not Select body)
        {
            throw ParserState.ExpectedCategoryException("view query", start);
        }
        return body;
    }

    public virtual CreateMaterializedView ParseCreateMaterializedView(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.MATERIALIZED, Keyword.VIEW);
        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        ObjectName name = _parseObjectName(state);

        SqlValueList<Identifier>? columns = state.ParseParenthesizedCommaSeparatedOptional(_parseIdentifier, false);

        Identifier? accessMethod = state.ParseKeyword(Keyword.USING) ? _parseIdentifier(state) : null;

        SqlValueList<PgStorageParameter>? storageParameters = state.ParseKeyword(Keyword.WITH)
            ? state.ParseParenthesizedCommaSeparated(_p.ComponentParser.ParseStorageParameter, false)
            : null;

        Identifier? tablespace = state.ParseKeyword(Keyword.TABLESPACE) ? _parseIdentifier(state) : null;

        state.ExpectKeyword(Keyword.AS);
        Select body = ParseViewQuery(state);

        bool? withData = null;
        Token populationToken = state.Peek();
        if (state.ParseKeyword(Keyword.WITH))
        {
            if (populationToken.PreNonSql?.Items.Any(nonSql => nonSql is Statements.Components.NonSql.Comment) == true)
            {
                body.Meta.PostNonSql = populationToken.PreNonSql.Items;
            }
            withData = !state.ParseKeyword(Keyword.NO);
            state.ExpectKeyword(Keyword.DATA);
        }

        return new CreateMaterializedView(name, body)
        {
            Columns = columns,
            IfNotExists = ifNotExists,
            AccessMethod = accessMethod,
            StorageParameters = storageParameters,
            Tablespace = tablespace,
            WithData = withData
        };
    }

    public virtual Statement ParseAlterMaterializedView(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.MATERIALIZED, Keyword.VIEW);
        if (state.ParseKeyword(Keyword.ALL))
        {
            state.ExpectKeywordsAll(Keyword.IN, Keyword.TABLESPACE);
            Identifier tablespace = _parseIdentifier(state);
            SqlValueList<Account>? ownedBy = state.ParseKeywordsAll(Keyword.OWNED, Keyword.BY)
                ? state.ParseCommaSeparated(ParsePostgresOwner)
                : null;
            state.ExpectKeywordsAll(Keyword.SET, Keyword.TABLESPACE);
            Identifier newTablespace = _parseIdentifier(state);
            return new AlterMaterializedViewsInTablespace(tablespace, newTablespace)
            {
                OwnedBy = ownedBy,
                Nowait = state.ParseKeyword(Keyword.NOWAIT)
            };
        }

        bool ifExists = state.ParseKeywordsAll(Keyword.IF, Keyword.EXISTS);

        ObjectName name = _parseObjectName(state);

        PgAlterRelationOperation first = ParseAlterMaterializedViewOperation(state, ifExists);
        SqlValueList<PgAlterRelationOperation> operations = [first];
        if (IsCombinableMaterializedViewOperation(first))
        {
            while (state.ConsumeTokenIs<Comma>())
            {
                PgAlterRelationOperation next = ParseAlterMaterializedViewOperation(state, ifExists);
                if (!IsCombinableMaterializedViewOperation(next))
                {
                    throw state.ExpectedCategoryException("combinable materialized-view action");
                }
                operations.Add(next);
            }
        }
        return new AlterMaterializedView(name, operations) { IfExists = ifExists };
    }

    private static bool IsCombinableMaterializedViewOperation(PgAlterRelationOperation operation)
    {
        return operation is not (PgAlterRelationOperation.RenameTo
            or PgAlterRelationOperation.RenameColumn
            or PgAlterRelationOperation.SetSchema
            or PgAlterRelationOperation.DependsOnExtension);
    }

    private PgAlterRelationOperation ParseAlterMaterializedViewOperation(ParserState state, bool ifExists)
    {
        PgAlterRelationOperation? shared = ParseOptionalAlterRelationOperation(state);
        if (shared != null)
        {
            return shared;
        }
        PgAlterRelationOperation operation;
        if (state.Peek() is Word { Keyword: Keyword.NO or Keyword.DEPENDS } && !ifExists)
        {
            bool no = state.ParseKeyword(Keyword.NO);
            state.ExpectKeywordsAll(Keyword.DEPENDS, Keyword.ON, Keyword.EXTENSION);
            operation = new PgAlterRelationOperation.DependsOnExtension(_parseIdentifier(state)) { No = no };
        }
        else if (state.ParseKeyword(Keyword.RENAME))
        {
            if (state.ParseKeyword(Keyword.TO))
            {
                operation = new PgAlterRelationOperation.RenameTo(_parseIdentifier(state));
            }
            else
            {
                bool useColumnKeyword = state.ParseKeyword(Keyword.COLUMN);
                Identifier oldName = _parseIdentifier(state);
                state.ExpectKeyword(Keyword.TO);
                operation = new PgAlterRelationOperation.RenameColumn(oldName, _parseIdentifier(state))
                {
                    UseColumnKeyword = useColumnKeyword
                };
            }
        }
        else if (state.ParseKeyword(Keyword.ALTER))
        {
            bool useColumnKeyword = state.ParseKeyword(Keyword.COLUMN);
            Identifier column = _parseIdentifier(state);
            operation = new PgAlterRelationOperation.AlterColumn(column, ParseColumnConfigurationOperation(state))
            {
                UseColumnKeyword = useColumnKeyword
            };
        }
        else
        {
            throw state.ExpectedException("RENAME", "SET", "RESET", "OWNER", "CLUSTER", "ALTER", "DEPENDS", "NO");
        }
        return operation;
    }

    public PgAlterRelationOperation? ParseOptionalAlterRelationOperation(ParserState state)
    {
        PgAlterRelationOperation operation;
        if (state.ParseKeyword(Keyword.SET))
        {
            if (state.PeekIs<ParenOpen>())
            {
                operation = new PgAlterRelationOperation.SetStorageParameters(
                    state.ParseParenthesizedCommaSeparated(_p.ComponentParser.ParseStorageParameter, false));
            }
            else if (state.ParseKeyword(Keyword.ACCESS))
            {
                state.ExpectKeyword(Keyword.METHOD);
                operation = state.ParseKeyword(Keyword.DEFAULT)
                    ? new PgAlterRelationOperation.SetDefaultAccessMethod()
                    : new PgAlterRelationOperation.SetAccessMethod(_parseIdentifier(state));
            }
            else if (state.ParseKeyword(Keyword.WITHOUT))
            {
                state.ExpectKeyword(Keyword.CLUSTER);
                operation = new PgAlterRelationOperation.SetWithoutCluster();
            }
            else
            {
                Keyword target = state.ParseKeywordsAny(Keyword.SCHEMA, Keyword.TABLESPACE);
                operation = target switch
                {
                    Keyword.SCHEMA => new PgAlterRelationOperation.SetSchema(_parseIdentifier(state)),
                    Keyword.TABLESPACE => new PgAlterRelationOperation.SetTablespace(_parseIdentifier(state)),
                    _ => throw state.ExpectedException("SCHEMA", "TABLESPACE", "ACCESS", "WITHOUT", "(")
                };
            }
        }
        else if (state.ParseKeyword(Keyword.RESET))
        {
            operation = new PgAlterRelationOperation.ResetStorageParameters(
                state.ParseParenthesizedCommaSeparated(_p.ComponentParser.ParseStorageParameterName, false));
        }
        else if (state.ParseKeyword(Keyword.OWNER))
        {
            state.ExpectKeyword(Keyword.TO);
            operation = new PgAlterRelationOperation.OwnerTo(ParsePostgresOwner(state));
        }
        else if (state.ParseKeyword(Keyword.CLUSTER))
        {
            state.ExpectKeyword(Keyword.ON);
            operation = new PgAlterRelationOperation.ClusterOn(_parseIdentifier(state));
        }
        else
        {
            return null;
        }
        return operation;
    }

    private Account ParsePostgresOwner(ParserState state)
    {
        Keyword role = state.ParseKeywordsAny(Keyword.CURRENT_ROLE, Keyword.CURRENT_USER, Keyword.SESSION_USER);
        return role switch
        {
            Keyword.CURRENT_ROLE => new Account.CurrentRole(),
            Keyword.CURRENT_USER => new Account.CurrentUser(),
            Keyword.SESSION_USER => new Account.SessionUser(),
            _ => new Account.Identity(_parseIdentifier(state))
        };
    }

    public PgColumnConfigurationOperation ParseColumnConfigurationOperation(ParserState state)
    {
        if (state.ParseKeyword(Keyword.RESET))
        {
            return new PgColumnConfigurationOperation.ResetOptions(state.ParseParenthesizedCommaSeparated(_parseIdentifier, false));
        }
        state.ExpectKeyword(Keyword.SET);
        if (state.PeekIs<ParenOpen>())
        {
            return new PgColumnConfigurationOperation.SetOptions(state.ParseParenthesizedCommaSeparated(ParseMaterializedViewColumnOption, false));
        }
        Keyword kind = state.ParseKeywordsAny(Keyword.STATISTICS, Keyword.STORAGE, Keyword.COMPRESSION);
        switch (kind)
        {
            case Keyword.STATISTICS:
                return state.ParseKeyword(Keyword.DEFAULT)
                    ? new PgColumnConfigurationOperation.SetDefaultStatistics()
                    : new PgColumnConfigurationOperation.SetStatistics(ValueParser.ParseLiteralInt(state));
            case Keyword.COMPRESSION:
                return state.ParseKeyword(Keyword.DEFAULT)
                    ? new PgColumnConfigurationOperation.SetDefaultCompression()
                    : new PgColumnConfigurationOperation.SetCompression(_parseIdentifier(state));
            case Keyword.STORAGE:
                Token start = state.Peek();
                Identifier mode = _parseIdentifier(state);
                if (mode.QuoteStyle != QuoteStyle.None || !PgColumnStorageMode.TryParse(mode.Name, out PgColumnStorageMode? storageMode))
                {
                    throw ParserState.ExpectedCategoryException("column storage mode", start);
                }
                return new PgColumnConfigurationOperation.SetStorage(storageMode);
            default:
                throw state.ExpectedException("STATISTICS", "STORAGE", "COMPRESSION", "(");
        }
    }

    private PgStorageParameter ParseMaterializedViewColumnOption(ParserState state)
    {
        PgStorageParameter parameter = _p.ComponentParser.ParseStorageParameter(state, maxParts: 1, requireValue: true, "materialized view column option");
        return parameter;
    }

    public virtual RefreshMaterializedView ParseRefreshMaterializedView(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.REFRESH, Keyword.MATERIALIZED, Keyword.VIEW);
        bool concurrently = state.ParseKeyword(Keyword.CONCURRENTLY);
        ObjectName name = _parseObjectName(state);
        bool? withData = null;
        if (state.ParseKeyword(Keyword.WITH))
        {
            Token populationToken = state.Peek();
            withData = !state.ParseKeyword(Keyword.NO);
            if (concurrently && !withData.Value)
            {
                throw ParserState.ExpectedException("DATA", populationToken);
            }
            state.ExpectKeyword(Keyword.DATA);
        }
        return new RefreshMaterializedView(name)
        {
            Concurrently = concurrently,
            WithData = withData
        };
    }

    private EventSchedule ParseEventSchedule(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.ON, Keyword.SCHEDULE);
        Keyword mode = state.ParseKeywordsAny(Keyword.AT, Keyword.EVERY);
        if (mode == Keyword.AT)
        {
            Expression expr = _p.ExpressionParser.ParseExpr(state);
            return new EventSchedule.At(expr);
        }
        if (mode == Keyword.EVERY)
        {
            Expression quantity = _p.ExpressionParser.ParseExpr(state);
            Token nextToken = state.Peek();
            DateTimeUnit unit = nextToken.ToDateTimeUnit() ?? throw state.ExpectedCategoryException("date_time_unit");
            state.Next();
            Expression? starts = state.ParseInit(state.ParseKeyword(Keyword.STARTS), _p.ExpressionParser.ParseExpr);
            Expression? ends = state.ParseInit(state.ParseKeyword(Keyword.ENDS), _p.ExpressionParser.ParseExpr);
            return new EventSchedule.Every(quantity, unit) { Start = starts, End = ends };
        }
        throw state.ExpectedException("AT", "EVERY");
    }

    private static EventEnabledStatus? ParseEventEnabledStatus(ParserState state)
    {
        if (state.ParseKeyword(Keyword.ENABLE))
        {
            return EventEnabledStatus.Enable;
        }
        if (state.ParseKeywordsAll(Keyword.DISABLE, Keyword.ON, Keyword.REPLICA))
        {
            return EventEnabledStatus.DisableOnReplica;
        }
        if (state.ParseKeywordsAll(Keyword.DISABLE, Keyword.ON, Keyword.SLAVE))
        {
            return EventEnabledStatus.DisableOnSlave;
        }
        if (state.ParseKeyword(Keyword.DISABLE))
        {
            return EventEnabledStatus.Disable;
        }
        return null;
    }

    public CreateEvent ParseCreateEvent(ParserState state, CreateOrLabel? createOrLabel, Definer? definer)
    {
        state.ExpectKeyword(Keyword.EVENT);
        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        ObjectName name = _parseObjectName(state);
        EventSchedule schedule = ParseEventSchedule(state);

        bool? onCompletionPreserve = null;
        if (state.ParseKeywordsAll(Keyword.ON, Keyword.COMPLETION, Keyword.NOT, Keyword.PRESERVE))
        {
            onCompletionPreserve = false;
        }
        else if (state.ParseKeywordsAll(Keyword.ON, Keyword.COMPLETION, Keyword.PRESERVE))
        {
            onCompletionPreserve = true;
        }

        EventEnabledStatus? enabledStatus = ParseEventEnabledStatus(state);

        Comment? comment = ComponentParser.ParseComment(state);

        state.ExpectKeyword(Keyword.DO);

        Statement body = _p.ParseStatement(state);

        return new CreateEvent(name, schedule, body)
        {
            CreateOrLabel = createOrLabel,
            Definer = definer,
            IfNotExists = ifNotExists,
            OnCompletionPreserve = onCompletionPreserve,
            EnabledStatus = enabledStatus,
            Comment = comment
        };
    }

    public CreateIndex ParseCreateIndex(ParserState state, CreateOrLabel? createOrLabel = null)
    {
        Keyword kindKw = state.ParseKeywordsAny(Keyword.UNIQUE, Keyword.FULLTEXT, Keyword.SPATIAL);
        bool supportsIndexOptions = kindKw is Keyword.undefined or Keyword.UNIQUE;

        IndexOrganization? indexOrganization = _p.TableParser.ParseOptionalIndexOrganization(state);

        state.ExpectKeyword(Keyword.INDEX);

        bool concurrently = supportsIndexOptions && SupportsPostgresIndexSyntax && state.ParseKeyword(Keyword.CONCURRENTLY);
        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        Identifier? indexName = SupportsPostgresIndexSyntax && !ifNotExists && state.PeekKeyword(Keyword.ON)
            ? null : _parseIdentifier(state);

        IndexMethod? method = null;

        if (supportsIndexOptions && state.ParseKeyword(Keyword.USING))
        {
            method = ParseCreateIndexMethod(state);
        }

        state.ExpectKeyword(Keyword.ON);
        bool only = supportsIndexOptions && SupportsPostgresIndexSyntax && state.ParseKeyword(Keyword.ONLY);
        ObjectName tableName = _parseObjectName(state);
        bool? indexMethodAfterTableName = null;
        if (supportsIndexOptions && state.PeekKeyword(Keyword.USING))
        {
            if (!SupportsIndexMethodAfterTableName)
            {
                throw state.ExpectedException("(");
            }
            if (method != null)
            {
                throw new ParseException.Duplicate("index_method".Italic(), state.Peek().Location);
            }
            state.Next();
            method = ParseCreateIndexMethod(state);
            indexMethodAfterTableName = true;
        }

        bool allowPgOptions = supportsIndexOptions && SupportsPostgresIndexSyntax;
        SqlValueList<KeyPart> columns = _p.ComponentParser.ParseParenthesizedKeyPartList(
            state, allowEmpty: false, allowLength: !allowPgOptions, allowPgOptions: allowPgOptions);

        IncludedColumns? includedColumns = null;
        Expression? filter = null;
        SqlValueList<StatementIndexOption>? options = null;
        IndexStorageLocation? storageLocation = null;
        bool? nullsDistinct = null;
        SqlValueList<PgStorageParameter>? storageParameters = null;
        Identifier? tablespace = null;

        if (supportsIndexOptions)
        {
            includedColumns = _p.TableParser.ParseOptionalIncludedColumns(state);
            if (SupportsPostgresIndexSyntax && state.ParseKeyword(Keyword.NULLS))
            {
                nullsDistinct = !state.ParseKeyword(Keyword.NOT);
                state.ExpectKeyword(Keyword.DISTINCT);
            }
            if (state.ParseKeyword(Keyword.WHERE))
            {
                filter = _p.ExpressionParser.ParseExpr(state);
            }

            if (filter == null || SupportsIndexOptionsAfterFilter)
            {
                options = ParseOptionalLegacyIndexOptions(state);
                if (SupportsPostgresIndexSyntax && options == null && state.ParseKeyword(Keyword.WITH))
                {
                    storageParameters = state.ParseParenthesizedCommaSeparated(_p.ComponentParser.ParseStorageParameter, false);
                }
                if (SupportsPostgresIndexSyntax && state.ParseKeyword(Keyword.TABLESPACE))
                {
                    tablespace = _parseIdentifier(state);
                }
                storageLocation = _p.TableParser.ParseOptionalIndexStorageLocation(state);
            }
            if (SupportsPostgresIndexSyntax && filter == null && state.ParseKeyword(Keyword.WHERE))
            {
                filter = _p.ExpressionParser.ParseExpr(state);
            }
        }

        if (supportsIndexOptions && state.PeekKeyword(Keyword.USING))
        {
            if (method != null)
            {
                throw new ParseException.Duplicate("index_method".Italic(), state.Peek().Location);
            }
            state.Next();
            method = ParseCreateIndexMethod(state);
            indexMethodAfterTableName = null;
        }

        Comment? comment = ComponentParser.ParseComment(state);

        PgCreateIndexOptions? pgOptions = concurrently || only || nullsDistinct != null || storageParameters != null || tablespace != null
            ? new PgCreateIndexOptions(concurrently, only)
            {
                NullsDistinct = nullsDistinct,
                StorageParameters = storageParameters,
                Tablespace = tablespace
            } : null;

        CreateIndex statement = kindKw switch
        {
            Keyword.UNIQUE => new CreateIndex.Unique(columns, indexName, tableName)
            {
                IndexMethodAfterTableName = indexMethodAfterTableName,
                PgOptions = pgOptions,
                IndexMethod = method,
                IndexOrganization = indexOrganization,
                IncludedColumns = includedColumns,
                Filter = filter,
                Options = options,
                StorageLocation = storageLocation,
                Comment = comment,
                IfNotExists = ifNotExists,
                CreateOrLabel = createOrLabel,
            },
            Keyword.FULLTEXT => new CreateIndex.FullText(columns, indexName, tableName)
            {
                Comment = comment,
                IfNotExists = ifNotExists,
                CreateOrLabel = createOrLabel,
            },
            Keyword.SPATIAL => new CreateIndex.Spatial(columns, indexName, tableName)
            {
                Comment = comment,
                IfNotExists = ifNotExists,
                CreateOrLabel = createOrLabel,
            },
            _ => new CreateIndex.Standard(columns, indexName, tableName)
            {
                IndexMethodAfterTableName = indexMethodAfterTableName,
                PgOptions = pgOptions,
                IndexMethod = method,
                IndexOrganization = indexOrganization,
                IncludedColumns = includedColumns,
                Filter = filter,
                Options = options,
                StorageLocation = storageLocation,
                Comment = comment,
                IfNotExists = ifNotExists,
                CreateOrLabel = createOrLabel,
            },
        };
        ValidateCreateIndex(state, statement);
        return statement;
    }

    private IndexMethod ParseCreateIndexMethod(ParserState state)
    {
        if (!SupportsPostgresIndexSyntax)
        {
            return new IndexMethod.BuiltIn(_p.TableParser.ParseIndexMethod(state));
        }
        BuiltInIndexMethod? builtIn = state.MaybeParseStringEnum<BuiltInIndexMethod>();
        if (builtIn is not null)
        {
            return new IndexMethod.BuiltIn(builtIn);
        }
        return new IndexMethod.Custom(_parseIdentifier(state));
    }

    protected virtual SqlValueList<StatementIndexOption>? ParseOptionalLegacyIndexOptions(ParserState state)
    {
        if (!state.PeekKeyword(Keyword.WITH))
        {
            return null;
        }
        if (!SupportsPostgresIndexSyntax)
        {
            return _p.TableParser.ParseIndexOptions(state);
        }
        return state.TryParse(_p.TableParser.ParseIndexOptions, out SqlValueList<StatementIndexOption>? options) ? options : null;
    }

    protected virtual void ValidateCreateIndex(ParserState state, CreateIndex statement)
    {
    }

    public CreateType ParseCreateType(ParserState state)
    {
        state.ExpectKeyword(Keyword.TYPE);
        ObjectName name = _p.ComponentParser.ParseObjectName(state, "type_name");
        state.ExpectKeyword(Keyword.AS);
        if (state.ParseKeyword(Keyword.ENUM))
        {
            return ParseCreateTypeEnumeratedSub(state, name);
        }
        var uniqueNames = new HashSet<string>(StringComparer.Ordinal);
        return new CreateType.Composite(name, state.ParseParenthesizedCommaSeparated(
            innerState => ParseCompositeTypeField(innerState, uniqueNames), allowEmpty: true));
    }

    private static CreateType.Enumerated ParseCreateTypeEnumeratedSub(ParserState state, ObjectName name)
    {

        state.ExpectLeftParen();
        if (state.ConsumeTokenIs<ParenClose>())
        {
            return new CreateType.Enumerated(name, []);
        }

        SqlValueList<string> labels = [];
        HashSet<string> uniqueLabels = [];

        while (true)
        {

            string label = state.Peek() is StringLiteral literal ? literal.Value : throw state.ExpectedCategoryException("literal_string");
            if (!uniqueLabels.Add(label))
            {
                throw new ParseException.Duplicate("enum label", state.Peek().Location);
            }
            labels.Add(label);
            state.Next();
            if (!state.ConsumeTokenIs<Comma>())
            {
                break;
            }
        }

        state.ExpectToken<ParenClose>();
        return new CreateType.Enumerated(name, labels);
    }

    private CompositeTypeField ParseCompositeTypeField(ParserState state, HashSet<string> uniqueNames)
    {
        Location location = state.Peek().Location;
        Identifier name = _p.ComponentParser.ParseIdentifier(state, "attribute_name");
        if (!uniqueNames.Add(name.Name))
        {
            throw new ParseException.Duplicate("composite attribute", location);
        }
        DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReference(state);
        ObjectName? collation = state.ParseKeyword(Keyword.COLLATE)
            ? _p.ComponentParser.ParseObjectName(state, "collation_name")
            : null;
        return new CompositeTypeField(name, dataType, collation);
    }

    public CreateDomain ParseCreateDomain(ParserState state)
    {
        state.ExpectKeyword(Keyword.DOMAIN);

        ObjectName name = _p.ComponentParser.ParseObjectName(state, "domain_name");

        bool printAs = state.ParseKeyword(Keyword.AS);

        DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReference(state);

        ObjectName? collation = null;
        Expression? defaultExpression = null;
        SqlValueList<StatementDomainConstraint> constraints = [];
        bool? notNull = null;

        while (true)
        {
            Location currentLocation = state.Peek().Location;
            if (state.ParseKeyword(Keyword.COLLATE))
            {
                if (collation != null)
                {
                    throw new ParseException.Duplicate("collation", currentLocation);
                }
                collation = _p.ComponentParser.ParseObjectName(state, "collation_name");
            }
            else if (state.ParseKeyword(Keyword.DEFAULT))
            {
                if (defaultExpression != null)
                {
                    throw new ParseException.Duplicate("default", currentLocation);
                }
                defaultExpression = _p.ExpressionParser.ParseDefaultExpr(state);
            }
            else
            {
                StatementDomainConstraint? constraint = ParseNextDomainConstraint(state);
                if (constraint != null)
                {
                    if (constraint is StatementDomainConstraint.Nullability nullability)
                    {
                        if (notNull.HasValue && (notNull.Value || nullability.NotNull))
                        {
                            throw new ParseException.Duplicate("nullability", currentLocation);
                        }
                        notNull = nullability.NotNull;
                    }
                    constraints.Add(constraint);
                }
                else
                {
                    break;
                }
            }
        }

        return new CreateDomain(name, dataType)
        {
            PrintAs = printAs,
            Collation = collation,
            Default = defaultExpression,
            Constraints = constraints
        };
    }

    private StatementDomainConstraint? ParseNextDomainConstraint(ParserState state)
    {
        Identifier? name = null;
        if (state.ParseKeyword(Keyword.CONSTRAINT))
        {
            name = _p.ComponentParser.ParseIdentifier(state, "constraint_name");
        }

        if (state.ParseKeyword(Keyword.CHECK))
        {
            return new StatementDomainConstraint.Check(_p.ExpressionParser.ParseDomainCheckExpr(state), name);
        }
        else if (state.ParseKeyword(Keyword.NOT))
        {
            state.ExpectKeyword(Keyword.NULL);
            return new StatementDomainConstraint.Nullability(true, name);
        }
        else if (state.ParseKeyword(Keyword.NULL))
        {
            return new StatementDomainConstraint.Nullability(false, name);
        }

        if (name != null)
        {
            throw state.ExpectedCategoryException("domain_constraint");
        }

        return null;
    }

    protected virtual bool SupportsDropObjectType(DroppableObject objectType) => true;

    protected virtual bool SupportsDropRoutineSignatures => true;

    protected virtual bool SupportsDropObjectList(DroppableObject objectType)
    {
        return objectType == DroppableObject.Table || objectType == DroppableObject.View
            || objectType == DroppableObject.MaterializedView || objectType == DroppableObject.Sequence
            || objectType == DroppableObject.Type || objectType == DroppableObject.Domain
            || objectType == DroppableObject.Index || objectType == DroppableObject.Schema
            || objectType == DroppableObject.Function || objectType == DroppableObject.Procedure
            || objectType == DroppableObject.Routine;
    }

    protected virtual bool ParseOptionalDropTemporary(ParserState state)
    {
        return state.ParseKeyword(Keyword.TEMPORARY);
    }

    protected virtual PgDropBehavior? ParseOptionalDropBehavior(ParserState state, DroppableObject objectType)
    {
        if (objectType == DroppableObject.Table || objectType == DroppableObject.View
            || objectType == DroppableObject.MaterializedView || objectType == DroppableObject.Sequence
            || objectType == DroppableObject.Type || objectType == DroppableObject.Domain
            || objectType == DroppableObject.Index || objectType == DroppableObject.Schema
            || objectType == DroppableObject.Function || objectType == DroppableObject.Procedure
            || objectType == DroppableObject.Routine || objectType == DroppableObject.Trigger)
        {
            return state.MaybeParseStringEnum<PgDropBehavior>();
        }
        return null;
    }

    protected virtual ObjectName? ParseDropIndexOnObject(ParserState state)
    {
        return state.ParseKeyword(Keyword.ON) ? _parseObjectName(state) : null;
    }

    protected virtual ObjectName? ParseDropTriggerOnObject(ParserState state)
    {
        return state.ParseKeyword(Keyword.ON) ? _parseObjectName(state) : null;
    }

    public Statement ParseDropObject(ParserState state)
    {
        state.ExpectKeyword(Keyword.DROP);

        bool temporary = ParseOptionalDropTemporary(state);

        Token typeToken = state.Peek();
        Keyword typeToDropKw = state.ParseKeywordsAny(Keyword.TABLE, Keyword.PROCEDURE, Keyword.FUNCTION, Keyword.ROUTINE, Keyword.TRIGGER, Keyword.VIEW, Keyword.MATERIALIZED, Keyword.EVENT, Keyword.SCHEMA, Keyword.DATABASE, Keyword.INDEX, Keyword.SEQUENCE, Keyword.TYPE, Keyword.DOMAIN);

        DroppableObject typeToDrop = typeToDropKw switch
        {
            Keyword.TABLE => DroppableObject.Table,
            Keyword.PROCEDURE => DroppableObject.Procedure,
            Keyword.FUNCTION => DroppableObject.Function,
            Keyword.ROUTINE => DroppableObject.Routine,
            Keyword.TRIGGER => DroppableObject.Trigger,
            Keyword.VIEW => DroppableObject.View,
            Keyword.MATERIALIZED => DroppableObject.MaterializedView,
            Keyword.SEQUENCE => DroppableObject.Sequence,
            Keyword.TYPE => DroppableObject.Type,
            Keyword.DOMAIN => DroppableObject.Domain,
            Keyword.EVENT => DroppableObject.Event,
            Keyword.SCHEMA => DroppableObject.Schema,
            Keyword.DATABASE => DroppableObject.Database,
            Keyword.INDEX => DroppableObject.Index,
            _ => throw state.ExpectedException<DroppableObject>()
        };

        if (!SupportsDropObjectType(typeToDrop))
        {
            throw ParserState.ExpectedCategoryException("DROP target", typeToken);
        }
        if (temporary && (typeToDrop == DroppableObject.MaterializedView || typeToDrop == DroppableObject.Sequence
            || typeToDrop == DroppableObject.Type || typeToDrop == DroppableObject.Domain
            || typeToDrop == DroppableObject.Function || typeToDrop == DroppableObject.Procedure
            || typeToDrop == DroppableObject.Routine))
        {
            throw ParserState.ExpectedCategoryException("non-temporary DROP target", typeToken);
        }
        if (typeToDrop == DroppableObject.MaterializedView)
        {
            state.ExpectKeyword(Keyword.VIEW);
        }

        bool ifExists = state.ParseKeywordsAll(Keyword.IF, Keyword.EXISTS);

        if (SupportsDropRoutineSignatures && (typeToDrop == DroppableObject.Function
            || typeToDrop == DroppableObject.Procedure || typeToDrop == DroppableObject.Routine))
        {
            SqlValueList<PgRoutineReference> routines = SupportsDropObjectList(typeToDrop)
                ? state.ParseCommaSeparated(ParseRoutineReference)
                : [ParseRoutineReference(state)];

            return new DropRoutine(routines, typeToDrop)
            {
                IfExists = ifExists,
                PgBehavior = ParseOptionalDropBehavior(state, typeToDrop)
            };
        }

        Func<ParserState, ObjectName> parseName = typeToDrop == DroppableObject.Trigger ? ParseTriggerName : _parseObjectName;
        SqlValueList<ObjectName> names = SupportsDropObjectList(typeToDrop)
            ? state.ParseCommaSeparated(parseName)
            : [parseName(state)];

        ObjectName? onObject = null;
        if (typeToDrop == DroppableObject.Index)
        {
            onObject = ParseDropIndexOnObject(state);
        }
        else if (typeToDrop == DroppableObject.Trigger)
        {
            onObject = ParseDropTriggerOnObject(state);
        }

        PgDropBehavior? pgBehavior = ParseOptionalDropBehavior(state, typeToDrop);

        return new DropObject(names, typeToDrop)
        {
            IfExists = ifExists,
            Temporary = temporary,
            OnObject = onObject,
            PgBehavior = pgBehavior
        };
    }

    private PgRoutineReference ParseRoutineReference(ParserState state)
    {
        ObjectName name = _parseObjectName(state);
        SqlValueList<StatementRoutineParameter>? parameters = null;
        if (state.PeekIs<ParenOpen>())
        {
            RoutineParamsSupportStyle style = RoutineParamsSupportStyle.PostgreSqlFunction with
            {
                DefaultSyntax = RoutineParameterDefaultSyntax.None,
                DefaultKinds = RoutineParameterDefaultKind.None
            };
            parameters = state.ParseParenthesizedCommaSeparated(
                parameterState => ParseRoutineParameter(parameterState, style), allowEmpty: true);
        }
        return new PgRoutineReference(name, parameters);
    }

    public Truncate ParseTruncateTable(ParserState state)
    {
        state.ExpectKeyword(Keyword.TRUNCATE);

        bool tableSpecified = state.ParseKeyword(Keyword.TABLE);
        ObjectName toTruncate = _parseObjectName(state);

        return new Truncate(toTruncate)
        {
            TableSpecified = tableSpecified
        };
    }
}
