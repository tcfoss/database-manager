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

    private readonly Parser _p;
    private readonly Func<ParserState, ObjectName> _parseObjectName;
    private readonly Func<ParserState, Identifier> _parseIdentifier;

    public DdlParser(Parser parser)
    {
        _p = parser;
        _parseObjectName = _p.ComponentParser.ParseObjectName;
        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
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
    /// Parses a single trigger event keyword (<c>INSERT</c>, <c>UPDATE</c>,
    /// <c>DELETE</c>).
    /// </summary>
    protected static TriggerEvent ParseTriggerEvent(ParserState state)
    {
        Keyword? next = (state.Next() as Word)?.Keyword;
        return next switch
        {
            Keyword.INSERT => TriggerEvent.Insert,
            Keyword.UPDATE => TriggerEvent.Update,
            Keyword.DELETE => TriggerEvent.Delete,
            _ => throw state.ExpectedException("INSERT", "UPDATE", "DELETE")
        };
    }

    protected virtual SqlValueList<TriggerEvent> ParseTriggerEvents(ParserState state)
    {
        return state.ParseCommaSeparated(ParseTriggerEvent);
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
        state.ExpectKeyword(Keyword.TRIGGER);

        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        ObjectName name = _parseObjectName(state);

        ObjectName? tableName = ParseOptionalTriggerOnTableBeforeTime(state);
        bool tableNameBeforeTimeAndEvent = tableName != null;

        TriggerTime triggerTime = ParseTriggerTime(state);
        SqlValueList<TriggerEvent> events = ParseTriggerEvents(state);

        if (!tableNameBeforeTimeAndEvent)
        {
            tableName = ParseTriggerOnTable(state);
        }

        TriggerExecutionQuantifier? executionQuantifier = ParseOptionalTriggerExecutionQuantifier(state);
        TriggerOrder? order = ParseOptionalTriggerOrder(state);

        bool triggerStartsWithAs = ParseTriggerBodyStartsWithAs(state);

        Statement body = _p.ParseStatement(state);

        return new CreateTrigger(name, triggerTime, events, tableName!, body)
        {
            Definer = definer,
            Order = order,
            CreateOrLabel = createOrLabel,
            IfNotExists = ifNotExists,
            TableNameBeforeTimeAndEvent = tableNameBeforeTimeAndEvent,
            ExecutionQuantifier = executionQuantifier,
            TriggerBodyStartsWithAs = triggerStartsWithAs
        };
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

    private static TriggerExecutionQuantifier.ForEach? ParseOptionalTriggerExecutionQuantifier(ParserState state)
    {
        if (!state.ParseKeywordsAll(Keyword.FOR, Keyword.EACH))
        {
            return null;
        }

        TriggerForEachType type = ParseTriggerForEachType(state);
        return new TriggerExecutionQuantifier.ForEach(type);
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

    private static RoutineParameterDirection? ParseRoutineParameterDirection(ParserState state)
    {
        RoutineParameterDirection? direction = state.Peek() switch
        {
            Word { Keyword: Keyword.IN } => RoutineParameterDirection.In,
            Word { Keyword: Keyword.OUT } => RoutineParameterDirection.Out,
            Word { Keyword: Keyword.INOUT } => RoutineParameterDirection.InOut,
            _ => null
        };

        if (direction != null)
        {
            state.Next();
        }

        return direction;
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

    protected virtual SqlValueList<StatementRoutineParameter> ParseRoutineParameterList(ParserState state)
    {
        return state.ParseParenthesizedCommaSeparated(ParseRoutineParameter, allowEmpty: true);
    }

    protected virtual StatementRoutineParameter ParseRoutineParameter(ParserState state)
    {
        RoutineParameterDirection? direction = ParseRoutineParameterDirection(state);
        Identifier name = _parseIdentifier(state);
        DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReference(state);

        return new StatementRoutineParameter.Directed(name, dataType, direction);
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
        SqlValueList<StatementRoutineParameter> parameters = ParseRoutineParameterList(state);

        state.ExpectKeyword(Keyword.RETURNS);
        DataTypeReference returnType = _p.DataTypeParser.ParseDataTypeReference(state);
        ParsedRoutineTail tail = ParseRoutineTail(state, isFunction: true);

        return new CreateFunction(name, parameters, returnType, tail.Body)
        {
            Definer = definer,
            CreateOrLabel = createOrLabel,
            Aggregate = aggregate,
            MyCharacteristic = tail.MyCharacteristic,
            MsOptions = tail.MsOptions,
            PgOptions = tail.PgOptions,
            RoutineTailMeta = tail.Meta,
            UseAsBeforeBody = tail.UseAsBeforeBody,
            IfNotExists = ifNotExists
        };
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
        SqlValueList<StatementRoutineParameter> parameters = ParseRoutineParameterList(state);

        ParsedRoutineTail tail = ParseRoutineTail(state, isFunction: false);

        return new CreateProcedure(name, parameters, tail.Body)
        {
            Definer = definer,
            CreateOrLabel = createOrLabel,
            MyCharacteristic = tail.MyCharacteristic,
            MsOptions = tail.MsOptions,
            PgOptions = tail.PgOptions,
            RoutineTailMeta = tail.Meta,
            UseAsBeforeBody = tail.UseAsBeforeBody,
            IfNotExists = ifNotExists
        };
    }

    protected record ParsedRoutineTail(Statement Body, bool UseAsBeforeBody)
    {
        public MyRoutineCharacteristic? MyCharacteristic { get; init; }
        public MsRoutineWithOptions? MsOptions { get; init; }
        public PgRoutineOptions? PgOptions { get; init; }
        public MetaData Meta { get; init; } = new();
    }

    protected virtual bool UseSqlRoutineTail(ParserState state)
    {
        return state.PeekKeywordsAllEqual(Keyword.BEGIN, Keyword.ATOMIC)
            || (state.PeekKeyword(Keyword.AS) && state.PeekNth(1) is StringLiteral or DollarQuotedStringLiteral)
            || (state.PeekKeyword(Keyword.LANGUAGE)
                && (state.PeekNth(2) is Word { Keyword: Keyword.AS }
                    || (state.PeekNth(2) is Word { Keyword: Keyword.BEGIN }
                        && state.PeekNth(3) is Word { Keyword: Keyword.ATOMIC })));
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

    protected virtual PgRoutineOptions? ParseOptionalPgRoutineOptions(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.LANGUAGE))
        {
            return null;
        }
        return new PgRoutineOptions(_parseIdentifier(state));
    }

    protected ParsedRoutineTail ParseSqlRoutineTail(ParserState state, bool isFunction)
    {
        int start = state.Peek().Location.Position;
        PgRoutineOptions? options = ParseOptionalPgRoutineOptions(state);
        if (options != null && state.PeekKeyword(Keyword.LANGUAGE))
        {
            throw new ParseException.Duplicate("LANGUAGE", state.Peek().Location);
        }
        bool useAsBeforeBody = state.ParseKeyword(Keyword.AS);
        Statement body;
        if (useAsBeforeBody)
        {
            if (state.Peek() is not (StringLiteral or DollarQuotedStringLiteral))
            {
                throw state.ExpectedException("string literal");
            }
            using (state.EnterStringLiteralAsStatement())
            {
                body = _p.ParseStatement(state);
            }
        }
        else if (state.PeekKeywordsAllEqual(Keyword.BEGIN, Keyword.ATOMIC))
        {
            using (state.EnterBeginAtomic())
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

        if (state.PeekKeyword(Keyword.LANGUAGE))
        {
            if (options != null)
            {
                throw new ParseException.Duplicate("LANGUAGE", state.Peek().Location);
            }
            options = ParseOptionalPgRoutineOptions(state);
        }
        if (state.PeekKeyword(Keyword.LANGUAGE))
        {
            throw new ParseException.Duplicate("LANGUAGE", state.Peek().Location);
        }
        if (body is RoutineBodyLiteral)
        {
            if (options == null)
            {
                throw state.ExpectedException("LANGUAGE");
            }
        }
        else if (options != null && options.Language.Name != "sql"
            && (options.Language.QuoteStyle != QuoteStyle.None
                || !string.Equals(options.Language.Name, "sql", StringComparison.OrdinalIgnoreCase)))
        {
            throw state.ExpectedException("LANGUAGE SQL");
        }

        return new ParsedRoutineTail(body, useAsBeforeBody)
        {
            PgOptions = options,
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
                defaultExpression = _p.ExpressionParser.ParseExpr(state);
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
