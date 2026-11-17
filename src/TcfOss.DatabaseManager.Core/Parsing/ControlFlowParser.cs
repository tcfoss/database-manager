using System.Globalization;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.PgDeclarations;
using NonSql = TcfOss.DatabaseManager.Core.Statements.Components.NonSql;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class ControlFlowParser
{
    private readonly ParserBase _p;
    private readonly Func<ParserState, Identifier> _parseIdentifier;
    private readonly Func<ParserState, ObjectName> _parseObjectName;
    private readonly Func<ParserState, Expression> _parseExpr;

    public ControlFlowParser(ParserBase parser)
    {
        _p = parser;
        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
        _parseExpr = _p.ExpressionParser.ParseExpr;
        _parseObjectName = _p.ComponentParser.ParseObjectName;
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

    public BeginEnd ParsePgPlBeginEnd(ParserState state)
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
                Statement declaration = IsPgCursorDeclaration(state)
                    ? ParsePgCursorDeclaration(state)
                    : ParsePgBlockVariable(state);
                ParserBase.AttachStatementMetadata(declaration, start, state);
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

    private SubVariableDeclaration ParsePgBlockVariable(ParserState state)
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
        return new SubVariableDeclaration(name, dataType)
        {
            Constant = constant,
            Collation = collation,
            NotNull = notNull,
            Default = def
        };
    }

    private static bool IsPgCursorDeclaration(ParserState state)
    {
        if (state.Peek() is not Word)
        {
            return false;
        }
        return state.PeekNth(1) is Word { Keyword: Keyword.CURSOR or Keyword.SCROLL }
            || state.PeekNth(1) is Word { Keyword: Keyword.NO }
                && state.PeekNth(2) is Word { Keyword: Keyword.SCROLL };
    }

    public SubCursorDeclaration ParsePgCursorDeclaration(ParserState state)
    {
        Identifier name = _parseIdentifier(state);
        PgCursorScrollMode? scrollMode = ControlFlowComponentHelpers.ParsePgCursorScrollMode(state);
        state.ExpectKeyword(Keyword.CURSOR);
        SqlValueList<PgCursorParameter>? parameters = state.PeekIs<ParenOpen>()
            ? state.ParseParenthesizedCommaSeparated(
                innerState => new PgCursorParameter(
                    _parseIdentifier(innerState),
                    _p.DataTypeParser.ParseDataTypeReference(innerState)),
                allowEmpty: false)
            : null;
        bool useIs = state.ParseKeyword(Keyword.IS);
        if (!useIs)
        {
            state.ExpectKeyword(Keyword.FOR);
        }
        Statement query = ParsePgRowReturningQuery(state);
        return new SubCursorDeclaration(name, scrollMode, parameters, useIs, query);
    }

    public PgExecute ParsePgExecute(ParserState state)
    {
        state.ExpectKeyword(Keyword.EXECUTE);
        Expression command = _parseExpr(state);
        SqlValueList<ObjectName>? into = null;
        bool strict = false;
        if (state.ParseKeyword(Keyword.INTO))
        {
            strict = state.ParseKeyword(Keyword.STRICT);
            into = state.ParseCommaSeparated(_p.ComponentParser.ParseObjectName);
        }
        SqlValueList<Expression>? parameters = state.ParseKeyword(Keyword.USING)
            ? state.ParseCommaSeparated(_parseExpr)
            : null;
        return new PgExecute(command, into, strict, parameters);
    }

    public Commit ParseCommit(ParserState state)
    {
        bool asEnd = state.ParseKeyword(Keyword.END);
        if (!asEnd)
        {
            state.ExpectKeyword(Keyword.COMMIT);
        }

        if (!asEnd
            && ControlFlowComponentHelpers.CanParsePgSqlTransaction(state)
            && state.ParseKeyword(Keyword.PREPARED))
        {
            return new Commit.Prepared(ValueParser.ParseStringValue(state));
        }

        TransactionNoun? noun = ParseOptionalTransactionNoun(state);
        TransactionOptions opts = ParseTransactionOptions(state);
        Identifier? name = ParseTransactionName(state, noun);

        return new Commit.Full()
        {
            Noun = noun,
            ChainOption = opts.Chain,
            ReleaseOption = opts.Release,
            Name = name,
            WriteAsEnd = asEnd
        };
    }

    public Rollback ParseRollback(ParserState state)
    {
        bool asAbort = state.ParseKeyword(Keyword.ABORT);
        if (!asAbort)
        {
            state.ExpectKeyword(Keyword.ROLLBACK);
        }

        if (!asAbort
            && ControlFlowComponentHelpers.CanParsePgSqlTransaction(state)
            && state.ParseKeyword(Keyword.PREPARED))
        {
            return new Rollback.Prepared(ValueParser.ParseStringValue(state));
        }

        TransactionNoun? noun = ParseOptionalTransactionNoun(state);
        if (!asAbort
            && ControlFlowComponentHelpers.CanParsePgSqlTransaction(state)
            && state.ParseKeyword(Keyword.TO))
        {
            bool useSavepoint = state.ParseKeyword(Keyword.SAVEPOINT);
            return new Rollback.ToSavepoint(_parseIdentifier(state))
            {
                Noun = noun,
                UseSavepoint = useSavepoint
            };
        }

        TransactionOptions opts = ParseTransactionOptions(state);
        Identifier? name = ParseTransactionName(state, noun);

        return new Rollback.Full()
        {
            Noun = noun,
            ChainOption = opts.Chain,
            ReleaseOption = opts.Release,
            Name = name,
            WriteAsAbort = asAbort
        };
    }

    public Statement ParsePrepare(ParserState state)
    {
        if (ControlFlowComponentHelpers.CanParsePgSqlTransaction(state)
            && state.PeekKeywordsAll(Keyword.PREPARE, Keyword.TRANSACTION)
            && state.PeekNth(2) is not Word { Keyword: Keyword.FROM })
        {
            return ParsePrepareTransaction(state);
        }
        if (state.My)
        {
            return ParsePrepareStatement(state);
        }
        throw ParserState.ExpectedException("SQL statement".Italic(), state.Peek());
    }

    private Prepare ParsePrepareStatement(ParserState state)
    {
        state.ExpectKeyword(Keyword.PREPARE);
        Identifier statementName = _parseIdentifier(state);
        state.ExpectKeyword(Keyword.FROM);

        if (state.Peek() is StringLiteral literal)
        {
            state.Next();
            return new Prepare.FromString(statementName, new Value.SingleQuotedString(literal.Value));
        }

        Identifier statementSource = _parseIdentifier(state);
        return new Prepare.FromVariable(statementName, statementSource);
    }

    public static PrepareTransaction ParsePrepareTransaction(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.PREPARE, Keyword.TRANSACTION);
        return new PrepareTransaction(ValueParser.ParseStringValue(state));
    }

    public Savepoint ParseSavepoint(ParserState state)
    {
        state.ExpectKeyword(Keyword.SAVEPOINT);
        return new Savepoint(_parseIdentifier(state));
    }

    public Release ParseReleaseSavepoint(ParserState state)
    {
        state.ExpectKeyword(Keyword.RELEASE);
        bool useSavepoint = state.ParseKeyword(Keyword.SAVEPOINT);
        return new Release(_parseIdentifier(state))
        {
            UseSavepoint = useSavepoint
        };
    }

    public virtual TransactionNoun? ParseOptionalTransactionNoun(ParserState state)
    {
        return state.MaybeParseStringEnum<TransactionNoun>();
    }

    public StartTransaction ParseStartTransaction(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.START, Keyword.TRANSACTION);
        SqlValueList<TransactionMode> modes = ParseTransactionModes(state, requireComma: !state.Pg);
        return new StartTransaction
        {
            Modes = modes
        };
    }

    public StartTransaction.Begin ParsePgBeginTransaction(ParserState state)
    {
        state.ExpectKeyword(Keyword.BEGIN);
        TransactionNoun? noun = ParseOptionalTransactionNoun(state);
        return new StartTransaction.Begin(noun)
        {
            Modes = ParseTransactionModes(state, false)
        };
    }

    public SetConstraints ParseSetConstraints(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.SET, Keyword.CONSTRAINTS);
        SqlValueList<ObjectName>? names = state.ParseKeyword(Keyword.ALL)
            ? null : state.ParseCommaSeparated(ParseConstraintName);
        bool deferred = state.ParseKeywordsAny(Keyword.DEFERRED, Keyword.IMMEDIATE) switch
        {
            Keyword.DEFERRED => true,
            Keyword.IMMEDIATE => false,
            _ => throw state.ExpectedException("DEFERRED", "IMMEDIATE")
        };
        return names == null ? new SetConstraints.All(deferred) : new SetConstraints.Named(names, deferred);
    }

    private ObjectName ParseConstraintName(ParserState state)
    {
        if (state.PeekKeyword(Keyword.ALL))
        {
            throw state.ExpectedException("constraint name");
        }
        return _parseObjectName(state);
    }

    public SetTransaction ParseSetTransaction(ParserState state)
    {
        state.ExpectKeyword(Keyword.SET);
        bool session = state.ParseKeyword(Keyword.SESSION);
        if (session)
        {
            state.ExpectKeywordsAll(Keyword.CHARACTERISTICS, Keyword.AS);
        }
        state.ExpectKeyword(Keyword.TRANSACTION);
        if (!session && state.ParseKeyword(Keyword.SNAPSHOT))
        {
            return new SetTransaction.Snapshot(ValueParser.ParseStringValue(state));
        }

        SqlValueList<TransactionMode> modes = ParseTransactionModes(state, requireComma: false);
        if (modes.Count == 0)
        {
            throw state.ExpectedException("transaction mode");
        }
        return new SetTransaction.Characteristics(modes)
        {
            Session = session
        };
    }

    protected SqlValueList<TransactionMode> ParseTransactionModes(ParserState state, bool requireComma)
    {
        SqlValueList<TransactionMode> modes = [];
        bool expectAnother = false;
        while (true)
        {
            TransactionMode? mode = ParseOptionalTransactionMode(state);
            if (mode == null)
            {
                break;
            }
            modes.Add(mode);
            expectAnother = state.ConsumeTokenIs<Comma>();
            if (requireComma && !expectAnother)
            {
                break;
            }
        }
        if (expectAnother)
        {
            throw state.ExpectedException("transaction mode");
        }
        return modes;
    }

    protected virtual TransactionMode? ParseOptionalTransactionMode(ParserState state)
    {
        TransactionMode? mode = ParseOptionalMyTransactionMode(state);
        if (mode != null)
        {
            return mode;
        }
        return ParseOptionalPgTransactionMode(state);
    }

    protected static TransactionMode? ParseOptionalMyTransactionMode(ParserState state)
    {
        if (state.ParseKeywordsAll(Keyword.WITH, Keyword.CONSISTENT, Keyword.SNAPSHOT))
        {
            return TransactionMode.WithConsistentSnapshot;
        }
        if (state.ParseKeyword(Keyword.READ))
        {
            if (state.ParseKeyword(Keyword.ONLY))
            {
                return TransactionMode.ReadOnly;
            }
            if (state.ParseKeyword(Keyword.WRITE))
            {
                return TransactionMode.ReadWrite;
            }
            throw state.ExpectedException("ONLY", "WRITE");
        }
        return null;
    }

    protected static TransactionMode? ParseOptionalPgTransactionMode(ParserState state)
    {
        if (state.ParseKeyword(Keyword.ISOLATION))
        {
            state.ExpectKeyword(Keyword.LEVEL);
            if (state.ParseKeyword(Keyword.SERIALIZABLE))
            {
                return TransactionMode.IsolationLevelSerializable;
            }
            if (state.ParseKeyword(Keyword.REPEATABLE))
            {
                state.ExpectKeyword(Keyword.READ);
                return TransactionMode.IsolationLevelRepeatableRead;
            }
            state.ExpectKeyword(Keyword.READ);
            if (state.ParseKeyword(Keyword.COMMITTED))
            {
                return TransactionMode.IsolationLevelReadCommitted;
            }
            state.ExpectKeyword(Keyword.UNCOMMITTED);
            return TransactionMode.IsolationLevelReadUncommitted;
        }
        if (state.ParseKeyword(Keyword.READ))
        {
            if (state.ParseKeyword(Keyword.ONLY))
            {
                return TransactionMode.ReadOnly;
            }
            state.ExpectKeyword(Keyword.WRITE);
            return TransactionMode.ReadWrite;
        }
        if (state.ParseKeyword(Keyword.NOT))
        {
            state.ExpectKeyword(Keyword.DEFERRABLE);
            return TransactionMode.NotDeferrable;
        }
        return state.MaybeParseStringEnum<TransactionMode>();
    }


    public virtual TransactionNoun ParseRequiredTransactionNoun(ParserState state)
    {
        return state.ParseStringEnum<TransactionNoun>();
    }

    protected readonly record struct TransactionOptions(TransactionChainOption? Chain, TransactionReleaseOption? Release);
    protected virtual TransactionOptions ParseTransactionOptions(ParserState state)
    {
        TransactionChainOption? chain = null;
        TransactionReleaseOption? release = null;

        while (true)
        {
            if (chain == null && state.ParseKeyword(Keyword.AND))
            {
                bool negated = state.ParseKeyword(Keyword.NO);
                state.ExpectKeyword(Keyword.CHAIN);
                chain = new TransactionChainOption { Negated = negated };
            }
            else if (release == null && state.ParseKeyword(Keyword.NO))
            {
                state.ExpectKeyword(Keyword.RELEASE);
                release = new TransactionReleaseOption { Negated = true };
            }
            else if (release == null && state.ParseKeyword(Keyword.RELEASE))
            {
                release = new TransactionReleaseOption { Negated = false };
            }
            else
            {
                break;
            }
        }
        return new TransactionOptions(chain, release);
    }

    public virtual Identifier? ParseTransactionName(ParserState state, TransactionNoun? noun)
    {
        if (noun is null)
        {
            return null;
        }
        if (state.Peek() is Word { Keyword: Keyword.undefined or Keyword.PREPARED })
        {
            return _parseIdentifier(state);
        }
        return null;
    }

    public PgOpen ParsePgOpen(ParserState state)
    {
        state.ExpectKeyword(Keyword.OPEN);
        Identifier cursorName = _parseIdentifier(state);
        PgCursorScrollMode? scrollMode = ControlFlowComponentHelpers.ParsePgCursorScrollMode(state);
        if (state.ParseKeyword(Keyword.FOR))
        {
            if (state.ParseKeyword(Keyword.EXECUTE))
            {
                Expression command = _parseExpr(state);
                SqlValueList<Expression>? parameters = state.ParseKeyword(Keyword.USING)
                    ? state.ParseCommaSeparated(_parseExpr)
                    : null;
                return new PgOpen.ForExecute(cursorName, scrollMode, command, parameters);
            }
            Statement query = ParsePgRowReturningQuery(state);
            return new PgOpen.ForQuery(cursorName, scrollMode, query);
        }
        if (scrollMode != null)
        {
            throw state.ExpectedException("FOR");
        }
        SqlValueList<FunctionArgument>? arguments = state.PeekIs<ParenOpen>()
            ? ParsePgCursorArguments(state)
            : null;
        return new PgOpen.Bound(cursorName, arguments);
    }

    private SqlValueList<FunctionArgument> ParsePgCursorArguments(ParserState state)
    {
        state.ExpectLeftParen();
        FunctionArguments.List arguments = _p.ExpressionParser.ParseFunctionArgumentList(state);
        if (arguments.DuplicateTreatment != null
            || arguments.Clauses != null
            || arguments.Arguments?.Any(argument => !ControlFlowComponentHelpers.IsPgCursorArgument(argument)) == true)
        {
            throw state.ExpectedException("cursor argument list");
        }
        return arguments.Arguments ?? [];
    }

    public virtual Statement ParseFetch(ParserState state)
    {
        state.ExpectKeyword(Keyword.FETCH);
        CursorDirection? direction = ParseCursorDirection(state, allowMany: !state.PgPl);
        CursorNameConnector? connector = ParseCursorNameConnector(state, direction);

        bool global = false;
        if (state.Ms && state.ParseKeyword(Keyword.GLOBAL))
        {
            global = true;
        }

        Identifier cursorName = _parseIdentifier(state);

        SqlValueList<ObjectName>? targets = null;
        if (state.PgPl | state.My)
        {
            state.ExpectKeyword(Keyword.INTO);
            targets = state.ParseCommaSeparated(_p.ComponentParser.ParseObjectName);
        }
        else if (state.Ms && state.ParseKeyword(Keyword.INTO))
        {
            targets = state.ParseCommaSeparated(_p.ComponentParser.ParseObjectName);
        }
        return new Fetch(direction, connector, cursorName, targets)
        {
            Global = global
        };
    }

    public PgMove ParsePgMove(ParserState state)
    {
        state.ExpectKeyword(Keyword.MOVE);
        CursorDirection? direction = ParseCursorDirection(state, allowMany: true);
        CursorNameConnector? connector;
        if (direction != null && state.PgPl)
        {
            connector = state.ParseStringEnum<CursorNameConnector>();
        }
        else
        {
            connector = state.MaybeParseStringEnum<CursorNameConnector>();
        }
        return new PgMove(direction, connector, _parseIdentifier(state));
    }

    public Close ParseClose(ParserState state)
    {
        state.ExpectKeyword(Keyword.CLOSE);
        if (state.Pg && !state.PgPl && state.Peek() is Word { Keyword: Keyword.ALL })
        {
            state.Next();
            return new Close.All();
        }
        if (state.PgPl && state.PeekKeyword(Keyword.ALL))
        {
            throw state.ExpectedException("cursor_name");
        }

        bool global = false;
        if (state.Ms && state.Peek() is Word { Keyword: Keyword.GLOBAL })
        {
            global = true;
            state.Next();
        }

        Identifier thingToClose = _parseIdentifier(state);
        return new Close.Named(thingToClose) { Global = global };
    }

    protected virtual CursorDirection? ParseCursorDirection(ParserState state, bool allowMany)
    {
        CursorDirection? basic = state.Peek() switch
        {
            Word { Keyword: Keyword.NEXT } => new CursorDirection.Next(),
            Word { Keyword: Keyword.PRIOR } => new CursorDirection.Prior(),
            Word { Keyword: Keyword.FIRST } => new CursorDirection.First(),
            Word { Keyword: Keyword.LAST } => new CursorDirection.Last(),
            _ => null
        };
        if (basic is not null)
        {
            state.Next();
            return basic;
        }

        Keyword direction = state.ParseKeywordsAny(Keyword.ABSOLUTE, Keyword.RELATIVE);
        if (direction != Keyword.undefined)
        {
            Expression count;
            if (state.PgPl)
            {
                count = _p.ExpressionParser.ParseExpr(state, Precedence.Between);
            }
            else
            {
                count = new LiteralValue(new Value.Number(ValueParser.ParseLiteralInt(state).ToString(CultureInfo.InvariantCulture), false));
            }
            return direction switch
            {
                Keyword.ABSOLUTE => new CursorDirection.Absolute(count),
                Keyword.RELATIVE => new CursorDirection.Relative(count),
                _ => throw new DefensiveGuardException("absolute/relative cursor direction", direction.ToString())
            };
        }

        direction = state.ParseKeywordsAny(Keyword.FORWARD, Keyword.BACKWARD);
        if (direction != Keyword.undefined)
        {
            PgCursorDirectionQuantifier? quantifier = null;
            if (allowMany && state.ParseKeyword(Keyword.ALL))
            {
                quantifier = new PgCursorDirectionQuantifier.All();
            }

            if (allowMany && quantifier is null)
            {
                Expression? count = null;

                if (state.PeekKeyword(Keyword.FROM) || state.PeekKeyword(Keyword.IN))
                {
                    // Do nothing, count remains null
                }
                else if (state.PgPl && state.TryParse(s => _p.ExpressionParser.ParseExpr(s, Precedence.Between), out count))
                {
                    // count is assigned
                }
                else if (state.TryParse(s => ValueParser.ParseNumericValue(s), out Value? numeric))
                {
                    count = new LiteralValue(numeric);
                }

                if (count is not null)
                {
                    quantifier = new PgCursorDirectionQuantifier.Specified(count);
                }
            }

            return direction switch
            {
                Keyword.FORWARD => new CursorDirection.Forward(quantifier),
                Keyword.BACKWARD => new CursorDirection.Backward(quantifier),
                _ => throw new DefensiveGuardException("forward/backward cursor direction", direction.ToString())
            };
        }

        if (allowMany && state.ParseKeyword(Keyword.ALL))
        {
            return new CursorDirection.Unnamed(new PgCursorDirectionQuantifier.All());
        }

        if (allowMany && state.TryParse(s => ValueParser.ParseNumericValue(s), out Value? bareNumeric))
        {
            return new CursorDirection.Unnamed(new PgCursorDirectionQuantifier.Specified(new LiteralValue(bareNumeric)));
        }

        return null;
    }

    protected virtual CursorNameConnector? ParseCursorNameConnector(ParserState state, CursorDirection? direction)
    {
        if (direction != null && state.PgPl)
        {
            return state.ParseStringEnum<CursorNameConnector>();
        }
        return state.MaybeParseStringEnum<CursorNameConnector>();
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

    public PgRaise ParsePgRaise(ParserState state)
    {
        state.ExpectKeyword(Keyword.RAISE);
        if (state.Peek() is Semicolon)
        {
            return new PgRaise.Rethrow();
        }

        PgRaiseLevel? level = state.MaybeParseStringEnum<PgRaiseLevel>();
        if (state.ParseKeyword(Keyword.USING))
        {
            return new PgRaise.WithOptions(level, ParsePgRaiseOptions(state));
        }

        if (state.ParseKeyword(Keyword.SQLSTATE))
        {
            var value = new Value.SingleQuotedString(ValueParser.ParseLiteralString(state));
            return new PgRaise.SqlState(level, value, ParsePgRaiseOptionsOptional(state));
        }
        if (state.Peek() is StringLiteral or EscapeStringLiteral or UnicodeStringLiteral or DollarQuotedStringLiteral)
        {
            var message = new Value.SingleQuotedString(ValueParser.ParseLiteralString(state));
            SqlValueList<Expression>? parameters = state.ParseInit(
                state.ConsumeTokenIs<Comma>(),
                innerState => innerState.ParseCommaSeparated(_parseExpr));
            return new PgRaise.Message(level, message, parameters, ParsePgRaiseOptionsOptional(state));
        }

        Identifier name = _parseIdentifier(state);
        return new PgRaise.ConditionName(level, name, ParsePgRaiseOptionsOptional(state));
    }

    private SqlValueList<PgRaiseOption>? ParsePgRaiseOptionsOptional(ParserState state)
    {
        return state.ParseInit(state.ParseKeyword(Keyword.USING), ParsePgRaiseOptions);
    }

    private SqlValueList<PgRaiseOption> ParsePgRaiseOptions(ParserState state)
    {
        return state.ParseCommaSeparated(innerState =>
        {
            PgRaiseOptionName name = innerState.ParseStringEnum<PgRaiseOptionName>();
            PgAssignmentOperator op = ParsePgAssignmentOperator(innerState);
            Expression value = _parseExpr(innerState);
            return new PgRaiseOption(name, op, value);
        });
    }

    public PgAssert ParsePgAssert(ParserState state)
    {
        state.ExpectKeyword(Keyword.ASSERT);
        Expression condition = _parseExpr(state);
        Expression? message = state.ParseInit(state.ConsumeTokenIs<Comma>(), _parseExpr);
        return new PgAssert(condition, message);
    }

    public PgGetDiagnostics ParsePgGetDiagnostics(ParserState state)
    {
        state.ExpectKeyword(Keyword.GET);
        if (state.ParseKeyword(Keyword.STACKED))
        {
            return ParsePgGetDiagnosticsStacked(state);
        }
        return ParsePgGetDiagnosticsCurrent(state);
    }

    private PgGetDiagnostics.Stacked ParsePgGetDiagnosticsStacked(ParserState state)
    {
        state.ExpectKeyword(Keyword.DIAGNOSTICS);
        SqlValueList<PgDiagnosticAssignment.Stacked> assignments = state.ParseCommaSeparated(s =>
        {
            ObjectName target = _p.ComponentParser.ParseObjectName(s);
            PgAssignmentOperator op = ParsePgAssignmentOperator(s);
            PgStackedDiagnosticItem item = s.ParseStringEnum<PgStackedDiagnosticItem>();
            return new PgDiagnosticAssignment.Stacked(target, op, item);
        });
        return new PgGetDiagnostics.Stacked(assignments);
    }

    private PgGetDiagnostics.Current ParsePgGetDiagnosticsCurrent(ParserState state)
    {
        bool printCurrent = state.ParseKeyword(Keyword.CURRENT);
        state.ExpectKeyword(Keyword.DIAGNOSTICS);
        SqlValueList<PgDiagnosticAssignment.Current> assignments = state.ParseCommaSeparated(s =>
        {
            ObjectName target = _p.ComponentParser.ParseObjectName(s);
            PgAssignmentOperator op = ParsePgAssignmentOperator(s);
            PgDiagnosticItem item = s.ParseStringEnum<PgDiagnosticItem>();
            return new PgDiagnosticAssignment.Current(target, op, item);
        });
        return new PgGetDiagnostics.Current(assignments) { PrintCurrent = printCurrent };
    }

    public static PgAssignmentOperator ParsePgAssignmentOperator(ParserState state)
    {
        if (state.ConsumeTokenIs<Equal>())
        {
            return PgAssignmentOperator.Equal;
        }
        if (state.ConsumeTokenIs<Walrus>())
        {
            return PgAssignmentOperator.Assignment;
        }
        throw state.ExpectedException("=", ":=");
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

    public PgForeach ParsePgForeach(ParserState state)
    {
        state.ExpectKeyword(Keyword.FOREACH);
        SqlValueList<ObjectName> targets = state.ParseCommaSeparated(_p.ComponentParser.ParseObjectName);
        int? slice = null;
        if (state.ParseKeyword(Keyword.SLICE))
        {
            if (state.Peek() is not NumericLiteral)
            {
                throw state.ExpectedCategoryException("literal_int");
            }
            slice = ValueParser.ParseLiteralInt(state);
        }
        state.ExpectKeywordsAll(Keyword.IN, Keyword.ARRAY);
        Expression arrayExpression = _parseExpr(state);
        Token loop = state.Peek();
        state.ExpectKeyword(Keyword.LOOP);
        SqlValueList<Statement> statements = ParseLoopStatements(state, Keyword.LOOP);
        return new PgForeach(targets, arrayExpression, statements)
        {
            Slice = slice,
            EndLabel = ParseLoopEndLabel(state),
            LoopMeta = new() { PreNonSql = loop.PreNonSql?.Items }
        };
    }

    public PgLoopControl ParsePgLoopContinue(ParserState state)
    {
        state.ExpectKeyword(Keyword.CONTINUE);
        Identifier? label = state.Peek() is Word { Keyword: not Keyword.WHEN } ? _parseIdentifier(state) : null;
        Expression? condition = state.ParseKeyword(Keyword.WHEN) ? _parseExpr(state) : null;
        return new PgLoopControl.Continue(label, condition);
    }

    public PgLoopControl ParsePgLoopExit(ParserState state)
    {
        state.ExpectKeyword(Keyword.EXIT);
        Identifier? label = state.Peek() is Word { Keyword: not Keyword.WHEN } ? _parseIdentifier(state) : null;
        Expression? condition = state.ParseKeyword(Keyword.WHEN) ? _parseExpr(state) : null;
        return new PgLoopControl.Exit(label, condition);
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

        if (state.TryParse(innerState =>
            {
                if (innerState.Peek() is not Word
                    {
                        Keyword: not (Keyword.SELECT or Keyword.VALUES or Keyword.WITH
                            or Keyword.INSERT or Keyword.UPDATE or Keyword.DELETE)
                    })
                {
                    return null;
                }
                Identifier name = _parseIdentifier(innerState);
                SqlValueList<FunctionArgument>? arguments = innerState.PeekIs<ParenOpen>()
                    ? ParsePgCursorArguments(innerState)
                    : null;
                return innerState.PeekKeyword(Keyword.LOOP)
                    ? new PgForSource.Cursor(name, arguments)
                    : null;
            }, out PgForSource.Cursor? cursorSource))
        {
            return cursorSource;
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
        Statement query = ParsePgRowReturningQuery(queryState);
        if (queryState.Peek() is not Eof)
        {
            throw queryState.ExpectedException("LOOP");
        }
        return new PgForSource.Query(query);
    }

    private Statement ParsePgRowReturningQuery(ParserState state)
    {
        Token queryStart = state.Peek();
        if (queryStart is not (Word
            {
                Keyword: Keyword.SELECT or Keyword.VALUES or Keyword.WITH
                    or Keyword.INSERT or Keyword.UPDATE or Keyword.DELETE
            } or ParenOpen))
        {
            throw state.ExpectedCategoryException("row-returning query");
        }
        Statement query = _p.ParseStatement(state);
        bool returnsRows = query switch
        {
            Select select => select.SelectInto == null && !select.Body.HasSelectInto(),
            Insert { Returning: not null } or Update { Returning: not null } or Delete { Returning: not null } => true,
            _ => false
        };
        if (!returnsRows)
        {
            throw ParserState.ExpectedCategoryException("row-returning query", queryStart);
        }
        return query;
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
            ParserBase.AttachStatementMetadata(elseIf, start, state);
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

    public virtual DeclareCursor ParseDeclareCursor(ParserState state)
    {
        state.ExpectKeyword(Keyword.DECLARE);
        Identifier name = _parseIdentifier(state);
        bool binary = false;
        PgCursorSensitivity? sensitivity = null;
        PgCursorScrollMode? scrollMode = null;
        PgCursorHoldMode? holdability;
        if (state.Pg)
        {
            binary = state.ParseKeyword(Keyword.BINARY);
            sensitivity = state.MaybeParseStringEnum<PgCursorSensitivity>();
            scrollMode = ControlFlowComponentHelpers.ParsePgCursorScrollMode(state);
        }
        state.ExpectKeyword(Keyword.CURSOR);
        holdability = state.Pg
            ? ControlFlowComponentHelpers.ParsePgCursorHoldMode(state)
            : null;
        state.ExpectKeyword(Keyword.FOR);
        Select query = _p.SelectParser.ParseSelect(state, null);

        return new DeclareCursor(name, query)
        {
            Binary = binary,
            Sensitivity = sensitivity,
            ScrollMode = scrollMode,
            Holdability = holdability
        };
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

    public virtual Statement ParseReturn(ParserState state)
    {
        state.ExpectKeyword(Keyword.RETURN);

        if (state.PgPl)
        {
            if (state.ParseKeyword(Keyword.NEXT))
            {
                if (_p.IsStatementTerminator(state.Peek(), state))
                {
                    return new ReturnNoTerminate.NextExpr(null);
                }
                return new ReturnNoTerminate.NextExpr(_parseExpr(state));
            }
            if (state.ParseKeyword(Keyword.QUERY))
            {
                Statement query = ParsePgRowReturningQuery(state);
                query.Meta.PostNonSql = null;
                return new ReturnNoTerminate.Query(query);
            }
        }

        if (AllowBareReturn(state) && _p.IsStatementTerminator(state.Peek(), state))
        {
            return new Return(null);
        }
        return new Return(_parseExpr(state));
    }

    private static bool AllowBareReturn(ParserState state)
    {
        return state.PgPl || state.Ms;
    }
}
