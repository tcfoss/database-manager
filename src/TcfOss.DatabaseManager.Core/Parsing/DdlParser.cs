using System.Diagnostics;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;
using TriggerOrder = TcfOss.DatabaseManager.Core.Statements.Components.TriggerOrder;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class DdlParser
{
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
    /// Parses a routine parameter list. Core requires parentheses; dialects
    /// (notably T-SQL) may override to allow a bare comma-separated form.
    /// </summary>
    protected virtual SqlValueList<RoutineParameter> ParseRoutineParameterList(ParserState state)
    {
        return state.ParseParenthesizedCommaSeparated(ParseRoutineParameter, allowEmpty: true);
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
            throw new UnreachableException();
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
        if (!state.ParseKeyword(Keyword.FOR))
        {
            return null;
        }

        bool includeEach = state.ParseKeyword(Keyword.EACH);

        TriggerForEachType type = ParseTriggerForEachType(state);
        return new TriggerExecutionQuantifier.ForEach(type, includeEach);
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

    protected virtual RoutineParameter ParseRoutineParameter(ParserState state)
    {
        RoutineParameterDirection? direction = ParseRoutineParameterDirection(state);
        Identifier name = _parseIdentifier(state);
        DataType dataType = _p.DataTypeParser.ParseDataType(state);

        return new RoutineParameter.Directed(name, dataType, direction);
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

    private static MyRoutineCharacteristic? ParseOptionalRoutineCharacteristic(ParserState state)
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
    private static MsRoutineWithOptions? ParseOptionalRoutineWithOptions(ParserState state)
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

        if (!withEncryption && !withRecompile && executeAs == null)
        {
            return null;
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
        SqlValueList<RoutineParameter> parameters = ParseRoutineParameterList(state);

        state.ExpectKeyword(Keyword.RETURNS);
        DataType returnType = _p.DataTypeParser.ParseDataType(state);
        MyRoutineCharacteristic? characteristic = ParseOptionalRoutineCharacteristic(state);

        MsRoutineWithOptions? msOptions = ParseOptionalRoutineWithOptions(state);
        if (msOptions is { Recompile: true })
        {
            // RECOMPILE is not valid for functions in any supported dialect.
            throw state.ExpectedException("ENCRYPTION", "EXECUTE AS", "AS");
        }

        bool useAsBeforeBody = state.ParseKeyword(Keyword.AS);
        Statement body = _p.ParseStatement(state);


        return new CreateFunction(name, parameters, returnType, body)
        {
            Definer = definer,
            CreateOrLabel = createOrLabel,
            Aggregate = aggregate,
            MyCharacteristic = characteristic,
            MsOptions = msOptions,
            UseAsBeforeBody = useAsBeforeBody,
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
        SqlValueList<RoutineParameter> parameters = ParseRoutineParameterList(state);

        MyRoutineCharacteristic? characteristic = ParseOptionalRoutineCharacteristic(state);

        MsRoutineWithOptions? msOptions = ParseOptionalRoutineWithOptions(state);

        bool useAsBeforeBody = state.ParseKeyword(Keyword.AS);
        Statement body = _p.ParseStatement(state);

        return new CreateProcedure(name, parameters, body)
        {
            Definer = definer,
            CreateOrLabel = createOrLabel,
            MyCharacteristic = characteristic,
            MsOptions = msOptions,
            UseAsBeforeBody = useAsBeforeBody,
            IfNotExists = ifNotExists
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

        SqlValueList<ObjectName> names;
        if (typeToDropKw is Keyword.TABLE or Keyword.VIEW)
        {
            names = state.ParseCommaSeparated(_parseObjectName);
        }
        else
        {
            names = [_parseObjectName(state)];
        }

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
