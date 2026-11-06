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
    protected virtual RoutineParamsSupportStyle GetRoutineParamsSupportStyle(bool isFunction) => RoutineParamsSupportStyle.KitchenSink;
    protected virtual bool SupportsOmittedFunctionReturn => true;
    protected virtual bool SupportsPostgresTriggerSyntax => true;
    protected virtual bool SupportsCommaSeparatedTriggerEvents => true;
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
        if (state.Peek() is not (StringLiteral or EscapeStringLiteral or DollarQuotedStringLiteral or Word { Keyword: Keyword.NULL }))
        {
            throw state.ExpectedException("string literal", "NULL");
        }
        return new CommentOn(target, ValueParser.ParseValue(state));
    }

    /// <summary>
    /// Parses a trigger time keyword. Core accepts all four variants
    /// (<c>BEFORE</c>, <c>AFTER</c>, <c>INSTEAD OF</c>, <c>FOR</c>); dialects
    /// override to narrow.
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
    /// Parses a trigger event, including PostgreSQL <c>UPDATE OF</c> columns
    /// and <c>TRUNCATE</c> when supported by the dialect.
    /// </summary>
    protected virtual TriggerEvent ParseTriggerEvent(ParserState state)
    {
        Keyword? next = (state.Next() as Word)?.Keyword;
        return next switch
        {
            Keyword.INSERT => new TriggerEvent.Insert(),
            Keyword.UPDATE => new TriggerEvent.Update(SupportsPostgresTriggerSyntax && state.ParseKeyword(Keyword.OF)
                ? state.ParseCommaSeparated(_parseIdentifier) : null),
            Keyword.DELETE => new TriggerEvent.Delete(),
            Keyword.TRUNCATE when SupportsPostgresTriggerSyntax => new TriggerEvent.Truncate(),
            _ => throw (SupportsPostgresTriggerSyntax
                ? state.ExpectedException("INSERT", "UPDATE", "DELETE", "TRUNCATE")
                : state.ExpectedException("INSERT", "UPDATE", "DELETE"))
        };
    }

    protected virtual SqlValueList<TriggerEvent> ParseTriggerEvents(ParserState state, out bool eventsUseOr)
    {
        List<TriggerEvent> events = [ParseTriggerEvent(state)];
        eventsUseOr = SupportsPostgresTriggerSyntax && state.ParseKeyword(Keyword.OR);
        if (eventsUseOr)
        {
            do
            {
                events.Add(ParseTriggerEvent(state));
            } while (state.ParseKeyword(Keyword.OR));
        }
        else if (SupportsCommaSeparatedTriggerEvents)
        {
            while (state.ConsumeTokenIs<Comma>())
            {
                events.Add(ParseTriggerEvent(state));
            }
        }
        return [.. events];
    }


    /// <summary>
    /// Parses an optional T-SQL <c>WITH</c> options block for views
    /// (<c>ENCRYPTION</c>, <c>SCHEMABINDING</c>). Returns <c>null</c> when no
    /// <c>WITH</c> is present.
    /// </summary>
    private static MsViewWithOptions? ParseOptionalViewWithOptions(ParserState state)
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
        ViewCheckOption? option = null;
        if (state.ParseKeyword(Keyword.WITH))
        {
            option = state.Peek() switch
            {
                Word { Keyword: Keyword.CASCADED } => ViewCheckOption.Cascaded,
                Word { Keyword: Keyword.LOCAL } => ViewCheckOption.Local,
                _ => throw state.ExpectedException<ViewCheckOption>()
            };
            state.Next();
            state.ExpectKeywordsAll(Keyword.CHECK, Keyword.OPTION);
        }
        return option;
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
        if (!state.PeekKeywordsAllEqual(Keyword.RETURNS, Keyword.NULL, Keyword.ON, Keyword.NULL, Keyword.INPUT)
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
        return state.PeekKeywordsAllEqual(Keyword.BEGIN, Keyword.ATOMIC)
            || (state.PeekKeyword(Keyword.AS) && state.PeekNth(1) is StringLiteral or EscapeStringLiteral or DollarQuotedStringLiteral)
            || state.Peek() is Word
            {
                Keyword: Keyword.IMMUTABLE or Keyword.STABLE
                or Keyword.VOLATILE or Keyword.STRICT or Keyword.CALLED or Keyword.SECURITY
                or Keyword.EXTERNAL or Keyword.PARALLEL or Keyword.COST or Keyword.ROWS or Keyword.SET
            }
            || state.PeekKeywordsAllEqual(Keyword.RETURNS, Keyword.NULL, Keyword.ON, Keyword.NULL, Keyword.INPUT);
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
            ObjectName key = _parseObjectName(state);
            if (state.ParseKeywordsAll(Keyword.FROM, Keyword.CURRENT))
            {
                return new PgStatementRoutineOption.RoutineSetting(new PgRoutineSetting.FromCurrent(key));
            }
            bool equalSign;
            if (state.ConsumeTokenIs<Equal>())
            {
                equalSign = true;
            }
            else if (state.ParseKeyword(Keyword.TO))
            {
                equalSign = false;
            }
            else
            {
                throw state.ExpectedException("=", "TO");
            }
            SqlValueList<Value> values = state.ParseCommaSeparated(ParseRoutineSettingValue);
            return new PgStatementRoutineOption.RoutineSetting(new PgRoutineSetting.ToValues(key, values)) { UseEqualSign = equalSign };

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

        Keyword volatility = state.ParseKeywordsAny(Keyword.IMMUTABLE, Keyword.STABLE, Keyword.VOLATILE);
        if (volatility != Keyword.undefined)
        {
            return new PgStatementRoutineOption.Volatility(PgRoutineVolatility.Parse(volatility.ToString()));
        }

        return null;
    }

    private static Value.Number ParsePositiveRoutineNumber(ParserState state)
    {
        state.ConsumeTokenIs<Plus>();
        Token token = state.Next();
        if (token is NumericLiteral { IsLong: false } number
            && double.TryParse(number.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value)
            && double.IsFinite(value) && value > 0)
        {
            return new Value.Number(number.Value, false);
        }
        throw ParserState.ExpectedException("positive number", token);
    }

    private static Value ParseRoutineSettingValue(ParserState state)
    {
        if (state.Peek() is Plus or Minus)
        {
            Token sign = state.Next();
            Token token = state.Next();
            if (token is NumericLiteral number)
            {
                return new Value.Number($"{sign}{number.Value}", number.IsLong);
            }
            throw ParserState.ExpectedException("number", token);
        }
        if (state.Peek() is Word { Keyword: not (Keyword.TRUE or Keyword.FALSE or Keyword.NULL) })
        {
            return new Value.SingleQuotedString(ValueParser.ParseLiteralStringOptionalQuotes(state));
        }
        return ValueParser.ParseValue(state);
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
            if (state.Peek() is not (StringLiteral or EscapeStringLiteral or DollarQuotedStringLiteral))
            {
                throw state.ExpectedException("string literal");
            }
            using (state.EnterContext(ParseContext.StringLiteralAsStatement))
            {
                body = _p.ParseStatement(state);
            }
        }
        else if (state.PeekKeywordsAllEqual(Keyword.BEGIN, Keyword.ATOMIC))
        {
            using (state.EnterContext(ParseContext.BeginAtomic))
            {
                body = _p.ParseStatement(state);
            }
        }
        else if (isFunction && state.PeekKeyword(Keyword.RETURN))
        {
            body = _p.ParseStatement(state);
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

    public virtual CreateView ParseCreateView(ParserState state, CreateOrLabel? createOrLabel, Definer? definer, ViewAlgorithm? viewAlgorithm, SecurityContext? securityContext)
    {
        state.ExpectKeyword(Keyword.VIEW);
        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        ObjectName name = _parseObjectName(state);
        SqlValueList<Identifier>? columns = state.ParseParenthesizedCommaSeparatedOptional(_parseIdentifier, false);

        MsViewWithOptions? msOptions = ParseOptionalViewWithOptions(state);

        state.ExpectKeyword(Keyword.AS);

        Select body = (Select)_p.ParseStatement(state);

        ViewCheckOption? viewCheckOption = ParseViewCheckOption(state);

        return new CreateView(name, body)
        {
            Columns = columns,
            CreateOrLabel = createOrLabel,
            IfNotExists = ifNotExists,
            Definer = definer,
            ViewAlgorithm = viewAlgorithm,
            SecurityContext = securityContext,
            ViewCheckOption = viewCheckOption,
            MsOptions = msOptions,
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
        bool allowIndexMethod = kindKw is Keyword.undefined or Keyword.UNIQUE;

        IndexOrganization? indexOrganization = _p.TableParser.ParseOptionalIndexOrganization(state);

        state.ExpectKeyword(Keyword.INDEX);

        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        Identifier indexName = _parseIdentifier(state);

        IndexMethod? method = null;
        if (allowIndexMethod && state.ParseKeyword(Keyword.USING))
        {
            method = _p.TableParser.ParseIndexMethod(state);
        }

        state.ExpectKeyword(Keyword.ON);
        ObjectName tableName = _parseObjectName(state);
        bool? indexMethodAfterTableName = null;
        if (allowIndexMethod && state.PeekKeyword(Keyword.USING))
        {
            if (!SupportsIndexMethodAfterTableName)
            {
                throw state.ExpectedException("(");
            }
            state.Next();
            if (method != null)
            {
                throw new ParseException.Duplicate("index_method".Italic(), state.Peek().Location);
            }
            method = _p.TableParser.ParseIndexMethod(state);
            indexMethodAfterTableName = true;
        }

        SqlValueList<KeyPart> columns = _p.ComponentParser.ParseParenthesizedKeyPartList(state, false);

        IncludedColumns? includedColumns = null;
        Expression? filter = null;
        SqlValueList<StatementIndexOption>? options = null;
        IndexStorageLocation? storageLocation = null;

        if (allowIndexMethod)
        {
            includedColumns = _p.TableParser.ParseOptionalIncludedColumns(state);
            if (state.ParseKeyword(Keyword.WHERE))
            {
                filter = _p.ExpressionParser.ParseExpr(state);
            }

            if (state.PeekKeyword(Keyword.WITH))
            {
                options = _p.TableParser.ParseIndexOptions(state);
            }

            storageLocation = _p.TableParser.ParseOptionalIndexStorageLocation(state);
        }

        if (allowIndexMethod && state.PeekKeyword(Keyword.USING))
        {
            if (method != null)
            {
                throw new ParseException.Duplicate("index_method".Italic(), state.Peek().Location);
            }
            state.Next();
            method = _p.TableParser.ParseIndexMethod(state);
        }

        Comment? comment = ComponentParser.ParseComment(state);

        return kindKw switch
        {
            Keyword.UNIQUE => new CreateIndex.Unique(columns, indexName, tableName)
            {
                IndexMethodAfterTableName = indexMethodAfterTableName,
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

    public DropObject ParseDropObject(ParserState state)
    {
        state.ExpectKeyword(Keyword.DROP);

        bool temporary = state.ParseKeyword(Keyword.TEMPORARY);

        Keyword typeToDropKw = state.ParseKeywordsAny(Keyword.TABLE, Keyword.PROCEDURE, Keyword.FUNCTION, Keyword.TRIGGER, Keyword.VIEW, Keyword.EVENT, Keyword.SCHEMA, Keyword.DATABASE, Keyword.INDEX);

        DroppableObject typeToDrop = typeToDropKw switch
        {
            Keyword.TABLE => DroppableObject.Table,
            Keyword.PROCEDURE => DroppableObject.Procedure,
            Keyword.FUNCTION => DroppableObject.Function,
            Keyword.TRIGGER => DroppableObject.Trigger,
            Keyword.VIEW => DroppableObject.View,
            Keyword.EVENT => DroppableObject.Event,
            Keyword.SCHEMA => DroppableObject.Schema,
            Keyword.DATABASE => DroppableObject.Database,
            Keyword.INDEX => DroppableObject.Index,
            _ => throw state.ExpectedException<DroppableObject>()
        };

        bool ifExists = state.ParseKeywordsAll(Keyword.IF, Keyword.EXISTS);

        SqlValueList<ObjectName> names = typeToDropKw is Keyword.TABLE or Keyword.VIEW
            ? state.ParseCommaSeparated(_parseObjectName)
            : [_parseObjectName(state)];

        ObjectName? onObject = null;
        if (typeToDrop == DroppableObject.Index)
        {
            state.ExpectKeyword(Keyword.ON);
            onObject = _parseObjectName(state);
        }

        return new DropObject(names, typeToDrop)
        {
            IfExists = ifExists,
            Temporary = temporary,
            OnObject = onObject
        };
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
