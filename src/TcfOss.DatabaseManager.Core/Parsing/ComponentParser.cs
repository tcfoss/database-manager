using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;
using DuplicateTreatment = TcfOss.DatabaseManager.Core.Expressions.Attributes.DuplicateTreatment;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class ComponentParser(Parser parser)
{
    private readonly Parser _p = parser;

    public Identifier ParseIdentifier(ParserState state)
    {
        return ParseIdentifier(state, "identifier");
    }


    public virtual Identifier ParseIdentifier(ParserState state, string expectedLabel)
    {
        Token token = state.Next();
        return token switch
        {
            Word word => word.ToIdentifier(state.SourceId),
            StringLiteral str => new Identifier(str.Value, _p.DefaultQuoteStyle) { Source = new SourceRef(state.SourceId, token.Location.Position, token.Location.Position + token.Length) },
            _ => throw ParserState.ExpectedCategoryException(expectedLabel, token)
        };
    }

    public static ExtendedIdentifier ParseExtendedIdentifier(ParserState state)
    {
        Token token = state.Next();
        return token switch
        {
            Word word => word.ToIdentifier(state.SourceId),
            StringLiteral str => new ExtendedIdentifier(str.Value, ExtendedQuoteStyle.SingleQuote) { Source = new SourceRef(state.SourceId, token.Location.Position, token.Location.Position + token.Length) },
            _ => throw ParserState.ExpectedCategoryException("identifier", token)
        };
    }

    public ObjectName ParseObjectName(ParserState state)
    {
        return ParseObjectName(state, "identifier");
    }

    public ObjectName ParseObjectName(ParserState state, string expectedLabel)
    {
        var idents = new SqlValueList<Identifier>();
        while (true)
        {
            Identifier ident = ParseIdentifier(state, expectedLabel);
            idents.Add(ident);

            if (!state.ConsumeTokenIs<Dot>())
            {
                break;
            }
        }

        return new ObjectName(idents);
    }

    /// <summary>
    /// Parses an object name with a bounded number of parts.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="minParts">The minimum number of parts. Must be at least 1.</param>
    /// <param name="maxParts">The maximum number of parts. Must be greater than or equal to minParts.</param>
    /// <param name="label">The label used in error messages.</param>
    /// <returns>The parsed object name.</returns>
    private ObjectName ParseBoundedObjectName(ParserState state, int minParts = 1, int maxParts = 2, string label = "object name")
    {
        SqlValueList<Identifier> idents = [];
        while (idents.Count < maxParts)
        {
            Identifier ident = ParseIdentifier(state, label);
            idents.Add(ident);

            if (idents.Count == maxParts || !state.ConsumeTokenIs<Dot>())
            {
                break;
            }
        }
        if (idents.Count < minParts)
        {
            throw ParserState.ExpectedCategoryException(label, state.Peek());
        }
        return new ObjectName(idents);
    }

    private KeyPart.Column ParseKeyPartColumn(ParserState state, bool permitLength, bool postgres, bool exclusion)
    {
        Identifier name = ParseIdentifier(state);
        uint? length = null;
        if (permitLength && state.ConsumeTokenIs<ParenOpen>())
        {
            length = ValueParser.ParseLiteralUInt(state);
            state.ExpectRightParen();
        }
        (PgIndexKeyOptions? options, Direction? direction) = ParseKeyPartTail(state, postgres);

        Token peeked = state.Peek();
        if (peeked is not Comma and not ParenClose && !(exclusion && state.PeekKeyword(Keyword.WITH)))
        {
            throw state.ExpectedException(")", ",");
        }
        return new KeyPart.Column(name, length, direction) { PgOptions = options };
    }

    private KeyPart ParseKeyPartInList(ParserState state, bool allowLength, bool postgres, bool exclusion = false)
    {
        if (state.TryParse(parserState => ParseKeyPartColumn(parserState, allowLength, postgres, exclusion), out KeyPart.Column? keyPartColumn))
        {
            return keyPartColumn;
        }

        Expression expr = _p.ExpressionParser.ParseExpr(state);
        (PgIndexKeyOptions? options, Direction? direction) = ParseKeyPartTail(state, postgres);

        return new KeyPart.IndexExpression(expr, direction) { PgOptions = options };
    }

    private (PgIndexKeyOptions? Options, Direction? Direction) ParseKeyPartTail(ParserState state, bool postgres)
    {
        ObjectName? operatorClass = null;
        SqlValueList<PgStorageParameter>? parameters = null;
        if (postgres && state.Peek() is Word { Keyword: not (Keyword.ASC or Keyword.DESC or Keyword.NULLS or Keyword.COLLATE or Keyword.WITH) })
        {
            operatorClass = ParseObjectName(state);
            if (state.PeekIs<ParenOpen>())
            {
                parameters = state.ParseParenthesizedCommaSeparated(s => ParseStorageParameter(s, maxParts: 1, requireValue: true, "operator class parameter = value"), false);
            }
        }
        Direction? direction = ParseDirection(state);
        NullsOrder? nullsOrder = postgres ? ParseOptionalNullsOrder(state) : null;

        PgIndexKeyOptions? options = operatorClass != null || nullsOrder != null
            ? new PgIndexKeyOptions(operatorClass, parameters, nullsOrder) : null;

        return (options, direction);
    }

    public SqlValueList<Identifier> ParseParenthisizedIdentifierList(ParserState state, bool allowEmpty)
    {
        return state.ParseParenthesizedCommaSeparated(ParseIdentifier, allowEmpty);
    }

    public SqlValueList<KeyPart> ParseParenthesizedKeyPartList(ParserState state, bool allowEmpty, bool allowLength = true, bool allowPgOptions = false)
    {
        return state.ParseParenthesizedCommaSeparated(parserState => ParseKeyPartInList(parserState, allowLength, allowPgOptions), allowEmpty);
    }

    public KeyPart ParseExclusionKeyPart(ParserState state)
    {
        return ParseKeyPartInList(state, allowLength: false, postgres: true, exclusion: true);
    }

    public static Direction? ParseDirection(ParserState state)
    {
        if (state.ParseKeyword(Keyword.ASC))
        {
            return Direction.Ascending;
        }
        if (state.ParseKeyword(Keyword.DESC))
        {
            return Direction.Descending;
        }

        return null;
    }

    public OrderBy ParseOrderBy(ParserState state)
    {
        Expression expr = _p.ExpressionParser.ParseExpr(state);
        Direction? dir = ParseDirection(state);

        return new OrderBy(expr, dir)
        {
            NullsOrder = ParseOptionalNullsOrder(state)
        };
    }

    /// <summary>
    /// <code><![CDATA[
    /// [ NULLS { FIRST | LAST } ]
    /// ]]></code>
    /// </summary>
    protected virtual NullsOrder? ParseOptionalNullsOrder(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.NULLS))
        {
            return null;
        }
        if (state.ParseKeyword(Keyword.FIRST))
        {
            return NullsOrder.First;
        }
        state.ExpectKeyword(Keyword.LAST);
        return NullsOrder.Last;
    }

    public DistinctFilter? ParseAllOrDistinct(ParserState state)
    {
        Keyword kw = state.ParseKeywordsAny(Keyword.ALL, Keyword.DISTINCT, Keyword.DISTINCTROW);

        if (kw == Keyword.DISTINCTROW)
        {
            return new DistinctFilter.DistinctRow();
        }
        if (kw == Keyword.ALL)
        {
            return new DistinctFilter.All();
        }

        if (kw != Keyword.DISTINCT)
        {
            return null;
        }

        bool on = state.ParseKeyword(Keyword.ON);

        if (!on)
        {
            return new DistinctFilter.Distinct();
        }

        state.ExpectLeftParen();

        if (state.ConsumeTokenIs<ParenClose>())
        {
            return new DistinctFilter.On([]);
        }


        SqlValueList<Expression> columnNames = state.ParseCommaSeparated(_p.ExpressionParser.ParseExpr);
        state.ExpectRightParen();

        return new DistinctFilter.On(columnNames);
    }

    public static DuplicateTreatment? ParseOptionalDuplicateTreatment(ParserState state)
    {
        if (state.ParseKeyword(Keyword.ALL))
        {
            return DuplicateTreatment.All;
        }
        if (state.ParseKeyword(Keyword.DISTINCT))
        {
            return DuplicateTreatment.Distinct;
        }
        return null;
    }

    private static Account ParseExplicitAccount(ParserState state)
    {
        ExtendedIdentifier ident = ParseExtendedIdentifier(state);
        if (state.PeekIs<At>())
        {
            state.Next();
            ExtendedIdentifier host = ParseExtendedIdentifier(state);
            return new Account.IdentityWithHost(ident, host);
        }
        else if (state.Peek() is Word { Sigil: SigilKind.Variable } w)
        {
            state.Next();
            return new Account.IdentityWithHost(ident, new Identifier(w.Value));
        }
        return new Account.Identity(ident);
    }

    public static Account ParseAccount(ParserState state)
    {
        Keyword account = state.ParseKeywordsAny(Keyword.CURRENT_USER, Keyword.CURRENT_ROLE, Keyword.SESSION_USER);

        return account switch
        {
            Keyword.CURRENT_USER => new Account.CurrentUser(),
            Keyword.CURRENT_ROLE => new Account.CurrentRole(),
            Keyword.SESSION_USER => new Account.SessionUser(),
            _ => ParseExplicitAccount(state)
        };
    }

    public static Definer? ParseOptionalDefiner(ParserState state)
    {
        if (state.ParseKeyword(Keyword.DEFINER))
        {
            state.ExpectToken<Equal>();
            return new Definer(ParseAccount(state));
        }
        return null;
    }

    private AssignmentTarget ParseAssignmentTarget(ParserState state)
    {
        if (state.ConsumeTokenIs<ParenOpen>())
        {
            SqlValueList<ObjectName> columns = state.ParseCommaSeparated(ParseObjectName);
            state.ExpectRightParen();
            return new AssignmentTarget.Tuple(columns);
        }
        return new AssignmentTarget.ObjectName(ParseObjectName(state));
    }

    public Assignment ParseAssignment(ParserState state)
    {
        AssignmentTarget target = ParseAssignmentTarget(state);
        state.ExpectToken<Equal>();
        Expression value = _p.ExpressionParser.ParseExpr(state);
        return new Assignment(target, value);
    }

    public virtual PgStatementSequenceOption? ParseSequenceOption(ParserState state, bool allowRestart = true)
    {
        if (allowRestart && state.ParseKeyword(Keyword.RESTART))
        {
            bool useWith = state.ParseKeyword(Keyword.WITH);
            long? value = useWith || state.Peek() is NumericLiteral or Plus or Minus
                ? ValueParser.ParseLiteralLong(state) : null;
            return new PgStatementSequenceOption.Restart(value) { UseWith = useWith };
        }
        if (state.ParseKeyword(Keyword.START))
        {
            bool useWith = state.ParseKeyword(Keyword.WITH);
            return new PgStatementSequenceOption.Start(ValueParser.ParseLiteralLong(state)) { UseWith = useWith };
        }
        if (state.ParseKeyword(Keyword.INCREMENT))
        {
            bool useBy = state.ParseKeyword(Keyword.BY);
            return new PgStatementSequenceOption.Increment(ValueParser.ParseLiteralLong(state)) { UseBy = useBy };
        }
        bool no = state.ParseKeyword(Keyword.NO);
        if (state.ParseKeyword(Keyword.MINVALUE))
        {
            return new PgStatementSequenceOption.MinValue(no ? null : ValueParser.ParseLiteralLong(state));
        }
        if (state.ParseKeyword(Keyword.MAXVALUE))
        {
            return new PgStatementSequenceOption.MaxValue(no ? null : ValueParser.ParseLiteralLong(state));
        }
        if (state.ParseKeyword(Keyword.CYCLE))
        {
            return new PgStatementSequenceOption.Cycle(!no);
        }
        if (!no && state.ParseKeyword(Keyword.CACHE))
        {
            return new PgStatementSequenceOption.Cache(ValueParser.ParseLiteralLong(state));
        }
        if (no)
        {
            throw state.ExpectedException("identity sequence option");
        }
        return null;
    }

    public virtual CreateOrLabel? ParseCreateOrLabel(ParserState state)
    {
        if (state.ParseKeywordsAll(Keyword.OR, Keyword.REPLACE))
        {
            return CreateOrLabel.OrReplace;
        }
        if (state.ParseKeywordsAll(Keyword.OR, Keyword.ALTER))
        {
            return CreateOrLabel.OrAlter;
        }
        return null;
    }

    public static Comment? ParseComment(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.COMMENT))
        {
            return null;
        }
        bool hasEquals = state.ConsumeTokenIs<Equal>();
        string commentValue = ValueParser.ParseLiteralString(state);
        if (hasEquals)
        {
            return new Comment.WithEqual(commentValue);
        }
        return new Comment(commentValue);
    }

    /// <summary>
    /// <code><![CDATA[
    /// [ LIMIT { expression | ALL } [ OFFSET expression [ ROW | ROWS ] ]
    ///     | OFFSET expression [ ROW | ROWS ] [ LIMIT { expression | ALL } ]
    ///     | [ OFFSET expression [ ROW | ROWS ] ] FETCH { FIRST | NEXT } [ expression ] { ROW | ROWS } { ONLY | WITH TIES }
    ///     | FETCH { FIRST | NEXT } [ expression ] { ROW | ROWS } { ONLY | WITH TIES } [ OFFSET expression [ ROW | ROWS ] ] ]
    /// ]]></code>
    /// </summary>
    public virtual Limit? ParseOptionalSelectLimit(ParserState state)
    {
        Expression? count = null;
        Expression? offset = null;
        bool hasLimit = false;
        bool hasOffset = false;
        bool fetch = false;
        bool withTies = false;
        while (true)
        {
            if (!hasLimit && state.ParseKeyword(Keyword.LIMIT))
            {
                hasLimit = true;
                count = state.ParseKeyword(Keyword.ALL) ? null : _p.ExpressionParser.ParseExpr(state);
                Limit? commaLimit = ParseOptionalCommaLimit(state, count);
                if (commaLimit != null)
                {
                    return commaLimit;
                }
            }
            else if (!hasLimit && state.ParseKeyword(Keyword.FETCH))
            {
                hasLimit = true;
                fetch = true;
                if (state.ParseKeywordsAny(Keyword.FIRST, Keyword.NEXT) == Keyword.undefined)
                {
                    throw state.ExpectedException("FIRST", "NEXT");
                }
                count = state.Peek() is Word { Keyword: Keyword.ROW or Keyword.ROWS }
                    ? null
                    : _p.ExpressionParser.ParseExpr(state);
                if (state.ParseKeywordsAny(Keyword.ROW, Keyword.ROWS) == Keyword.undefined)
                {
                    throw state.ExpectedException("ROW", "ROWS");
                }
                withTies = state.ParseKeyword(Keyword.WITH);
                state.ExpectKeyword(withTies ? Keyword.TIES : Keyword.ONLY);
            }
            else if (!hasOffset && state.ParseKeyword(Keyword.OFFSET))
            {
                hasOffset = true;
                offset = _p.ExpressionParser.ParseExpr(state);
                state.ParseKeywordsAny(Keyword.ROW, Keyword.ROWS);
            }
            else
            {
                break;
            }
        }

        if (fetch)
        {
            return new Limit.PgFetch(count, offset) { WithTies = withTies };
        }
        if (hasLimit)
        {
            return new Limit.PgLimit(count, offset);
        }

        return offset == null ? null : new Limit.PgOffset(offset);
    }

    /// <summary>
    /// <code><![CDATA[
    /// [ , expression ]
    /// ]]></code>
    /// </summary>
    protected virtual Limit? ParseOptionalCommaLimit(ParserState state, Expression? offset)
    {
        if (offset == null || !state.ConsumeTokenIs<Comma>())
        {
            return null;
        }
        return new Limit.MyCommaSeparated(_p.ExpressionParser.ParseExpr(state), offset);
    }

    public Limit ParseLimit(ParserState state)
    {
        state.ExpectKeyword(Keyword.LIMIT);
        if (state.PeekKeyword(Keyword.ALL))
        {
            throw ParserState.ExpectedCategoryException("limit expression", state.Peek());
        }
        Expression limitExpression = _p.ExpressionParser.ParseExpr(state);

        if (state.ConsumeTokenIs<Comma>())
        {
            Expression offsetExpression = limitExpression;
            limitExpression = _p.ExpressionParser.ParseExpr(state);
            return new Limit.MyCommaSeparated(limitExpression, offsetExpression);
        }

        if (state.ParseKeyword(Keyword.OFFSET))
        {
            Expression offsetExpression = _p.ExpressionParser.ParseExpr(state);
            return new Limit.MyLimitOffset(limitExpression, offsetExpression);
        }

        return new Limit.ExpressionLimit(limitExpression);
    }

    /// <summary>
    /// <code><![CDATA[
    /// OFFSET n { ROW | ROWS } [ FETCH { FIRST | NEXT } m { ROW | ROWS } ONLY ]
    /// ]]></code>
    /// </summary>
    public Limit.OffsetFetch ParseOffsetFetch(ParserState state)
    {
        state.ExpectKeyword(Keyword.OFFSET);
        Expression offset = _p.ExpressionParser.ParseExpr(state);
        if (state.ParseKeywordsAny(Keyword.ROW, Keyword.ROWS) == Keyword.undefined)
        {
            throw state.ExpectedException("ROW", "ROWS");
        }

        Expression? fetch = null;
        if (state.ParseKeyword(Keyword.FETCH))
        {
            if (state.ParseKeywordsAny(Keyword.FIRST, Keyword.NEXT) == Keyword.undefined)
            {
                throw state.ExpectedException("FIRST", "NEXT");
            }
            fetch = _p.ExpressionParser.ParseExpr(state);
            if (state.ParseKeywordsAny(Keyword.ROW, Keyword.ROWS) == Keyword.undefined)
            {
                throw state.ExpectedException("ROW", "ROWS");
            }
            state.ExpectKeyword(Keyword.ONLY);
        }

        return new Limit.OffsetFetch(offset, fetch);
    }

    /// <summary>
    /// <code><![CDATA[
    /// [ FOR lock_strength [ OF name [, ...] ] [ NOWAIT | SKIP LOCKED ] ]
    /// ]]></code>
    /// </summary>
    public virtual RowLockingClause? ParseOptionalRowLock(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.FOR))
        {
            return null;
        }

        RowLockStrength strength = ParseRowLockStrength(state);
        SqlValueList<Identifier>? of = state.ParseKeyword(Keyword.OF)
            ? state.ParseCommaSeparated(ParseIdentifier)
            : null;

        RowLockWait? wait = null;
        if (state.ParseKeyword(Keyword.NOWAIT))
        {
            wait = RowLockWait.Nowait;
        }
        else if (state.ParseKeyword(Keyword.SKIP))
        {
            state.ExpectKeyword(Keyword.LOCKED);
            wait = RowLockWait.SkipLocked;
        }

        return new RowLockingClause(strength)
        {
            Of = of,
            Wait = wait
        };
    }

    /// <summary>
    /// <code><![CDATA[
    /// UPDATE | NO KEY UPDATE | SHARE | KEY SHARE
    /// ]]></code>
    /// </summary>
    protected virtual RowLockStrength ParseRowLockStrength(ParserState state)
    {
        if (state.ParseKeyword(Keyword.NO))
        {
            state.ExpectKeyword(Keyword.KEY);
            state.ExpectKeyword(Keyword.UPDATE);
            return RowLockStrength.NoKeyUpdate;
        }
        if (state.ParseKeyword(Keyword.KEY))
        {
            state.ExpectKeyword(Keyword.SHARE);
            return RowLockStrength.KeyShare;
        }
        return ParseBasicRowLockStrength(state);
    }

    /// <summary>
    /// <code><![CDATA[
    /// UPDATE | SHARE
    /// ]]></code>
    /// </summary>
    protected static RowLockStrength ParseBasicRowLockStrength(ParserState state)
    {
        if (state.ParseKeyword(Keyword.UPDATE))
        {
            return RowLockStrength.Update;
        }
        state.ExpectKeyword(Keyword.SHARE);
        return RowLockStrength.Share;
    }

    public static SetQuantifier? ParseSetQuantifier(ParserState state)
    {
        if (state.ParseKeyword(Keyword.DISTINCT))
        {
            return SetQuantifier.Distinct;
        }
        if (state.ParseKeyword(Keyword.ALL))
        {
            return SetQuantifier.All;
        }
        return null;
    }

    public PgStorageParameter ParseStorageParameter(ParserState state, int maxParts, bool requireValue, string label = "storage parameter")
    {
        ObjectName name = ParseBoundedObjectName(state, 1, maxParts, label);
        Value? value;
        if (requireValue)
        {
            state.ExpectToken<Equal>();
            value = ParseSimpleValue(state);
        }
        else
        {
            value = state.ConsumeTokenIs<Equal>() ? ParseSimpleValue(state) : null;
        }
        return new PgStorageParameter(name, value);
    }

    public ObjectName ParseStorageParameterName(ParserState state)
    {
        return ParseBoundedObjectName(state, 1, 2, "storage parameter name");
    }

    public PgStorageParameter ParseStorageParameter(ParserState state)
    {
        return ParseStorageParameter(state, maxParts: 2, requireValue: false);
    }

    public static Value ParseSimpleValue(ParserState state)
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
}
