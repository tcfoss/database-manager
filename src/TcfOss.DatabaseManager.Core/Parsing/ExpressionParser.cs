using System.Diagnostics;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using BinaryOperatorExp = TcfOss.DatabaseManager.Core.Expressions.BinaryOperator;
using BinaryOperatorOp = TcfOss.DatabaseManager.Core.BuiltIn.BinaryOperator;
using UnaryOperatorExp = TcfOss.DatabaseManager.Core.Expressions.UnaryOperator;
using UnaryOperatorOp = TcfOss.DatabaseManager.Core.BuiltIn.UnaryOperator;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class ExpressionParser
{
    private readonly Parser _p;
    private readonly PrecedenceManager _pm;
    private readonly Func<ParserState, Identifier> _parseIdentifier;

    public ExpressionParser(Parser parser)
    {
        _p = parser;
        _pm = _p.PrecedenceManager;

        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
    }


    public Expression ParseExpr(ParserState state)
    {
        return ParseSubExpression(state, 0);
    }

    protected Expression ParseSubExpression(ParserState state, short precedence)
    {
        Expression expr = ParsePrefix(state);

        while (true)
        {
            short nextPrecedence = _pm.GetNextPrecedence(state);
            if (precedence >= nextPrecedence)
            {
                break;
            }
            expr = ParseInfix(state, expr, nextPrecedence);
        }

        return expr;
    }

    protected virtual Expression ParsePrefix(ParserState state)
    {
        if (state.Peek() is Word { Keyword: var kw } && _p.DataTypeParser.IsDataTypeKeyword(kw)
            && state.TryParse((s) =>
        {
            DataType dataType = _p.DataTypeParser.ParseDataType(s);
            return dataType switch
            {
                // TODO: Handle Interval, Custom
                _ => new TypedString(ValueParser.ParseLiteralString(s), dataType)
            };
        }, out TypedString? result))
        {
            return result;
        }

        Token token = state.Peek();

        Expression expr = token switch
        {
            Word { Keyword: Keyword.TRUE or Keyword.FALSE or Keyword.NULL } => ParseValueExpr(state),
            Word { Keyword: Keyword.CASE } => ParseCaseExpr(state),
            Word { Keyword: Keyword.NOT } => ParseNot(state),
            Word { Keyword: Keyword.EXISTS } => ParseExists(state, false),
            Word { Keyword: Keyword.POSITION } when state.PeekNthIs<ParenOpen>(1) => ParsePositionInOrFunction(state),
            Word { Keyword: Keyword.INTERVAL } => ParseInterval(state),
            Word { Keyword: Keyword.CONVERT } => ParseConvert(state),
            Word { Keyword: Keyword.CAST } => ParseCast(state),
            Minus or Plus => ParseUnaryExpr(state),
            NumericLiteral
                or StringLiteral
                or NationalStringLiteral => ParseValueExpr(state),
            Word => ParseMultipart(state),
            ParenOpen => ParseParenOpen(state),
            Exclamation => ParseNotExclamation(state),
            _ => throw ParserState.ExpectedException("expression".Italic(), token)
        };

        return expr;
    }

    private Expression ParseInfix(ParserState state, Expression left, short precedence)
    {
        Token token = state.Next();
        var op = token.ToBinaryOperator();

        if (op != null)
        {
            return ParseInfixBinaryOperator(state, token, op, left, precedence);
        }

        if (token is Word w)
        {
            return ParseInfixWord(state, w, left, precedence);
        }

        // TODO : (token is DoubleColon), (token is LeftBracket), ...
        throw new NotImplementedException();
    }

    private Expression ParseInfixBinaryOperator(ParserState state, Token token, BinaryOperatorOp op, Expression left, short precedence)
    {
        Keyword qualifierKeyword = state.ParseKeywordsAny(Keyword.ANY, Keyword.ALL, Keyword.SOME);
        if (qualifierKeyword != Keyword.undefined)
        {
            Expression right;
            state.ExpectLeftParen();

            if (PeekSubquery(state))
            {
                state.Rewind();
                right = ParseSubExpression(state, precedence);
            }
            else
            {
                right = ParseSubExpression(state, precedence);
                state.ExpectRightParen();
            }

            if (op != BinaryOperatorOp.GreaterThan
                    && op != BinaryOperatorOp.GreaterThanOrEqual
                    && op != BinaryOperatorOp.LessThan
                    && op != BinaryOperatorOp.LessThanOrEqual
                    && op != BinaryOperatorOp.Equal
                    && op != BinaryOperatorOp.NotEqual)
            {
                throw ParserState.ExpectedCategoryException("comparison_operator", token);
            }

            return qualifierKeyword switch
            {
                Keyword.ALL => new QuantifiedBinaryOperator(left, op, right, AggregateQuantifier.All),
                Keyword.ANY => new QuantifiedBinaryOperator(left, op, right, AggregateQuantifier.Any),
                Keyword.SOME => new QuantifiedBinaryOperator(left, op, right, AggregateQuantifier.Some),
                _ => throw new UnreachableException()
            };
        }

        // TODO: BinaryOperator.Custom

        return new BinaryOperatorExp(left, op, ParseSubExpression(state, precedence));
    }

    private Expression ParseInfixWord(ParserState state, Word word, Expression left, short precedence)
    {
        Keyword keyword = word.Keyword;

        if (keyword == Keyword.IS)
        {
            return ParseAfterIs(state, left);
        }
        if (keyword == Keyword.AT)
        {
            state.ExpectKeywordsAll(Keyword.TIME, Keyword.ZONE);
            return new AtTimeZone(left, ParseSubExpression(state, precedence));
        }
        if (keyword is Keyword.NOT or Keyword.IN or Keyword.BETWEEN
                or Keyword.LIKE or Keyword.ILIKE or Keyword.SIMILAR
                or Keyword.REGEXP or Keyword.RLIKE)
        {
            state.Rewind();
            bool negated = state.ParseKeyword(Keyword.NOT);

            if (state.ParseKeyword(Keyword.REGEXP))
            {
                return new Regexp(left, negated, ParseSubExpression(state, _pm.GetPrecedence(Precedence.Like)), false);
            }
            if (state.ParseKeyword(Keyword.RLIKE))
            {
                return new Regexp(left, negated, ParseSubExpression(state, _pm.GetPrecedence(Precedence.Like)), true);
            }
            if (state.ParseKeyword(Keyword.IN))
            {
                return ParseIn(state, left, negated);
            }
            if (state.ParseKeyword(Keyword.BETWEEN))
            {
                return ParseBetween(state, left, negated);
            }
            if (state.ParseKeyword(Keyword.LIKE))
            {
                return ParseLike(state, left, negated);
            }

            // TODO : Handle ILIKE, SIMILAR, etc

            throw state.ExpectedException("IN", "BETWEEN", "LIKE", "REGEXP", "RLIKE");
        }

        throw new NotImplementedException($"Infix operator not implemented for keyword '{keyword}'.");
    }

    private static bool PeekSubquery(ParserState state)
    {
        if (state.ParseKeywordsAny(Keyword.SELECT, Keyword.WITH) == Keyword.undefined)
        {
            return false;
        }
        state.Rewind();
        return true;
    }

    protected Expression ParseMultipart(ParserState state)
    {
        Word word = (state.Next() as Word)!;
        Debug.Assert(word is not null);

        Token token = state.Peek();

        if (token is ParenOpen or Dot)
        {
            var idParts = new SqlValueList<Identifier> { word.ToIdentifier(state.SourceId) };
            bool endsWithWildcard = false;

            while (state.ConsumeTokenIs<Dot>())
            {
                switch (state.Next())
                {
                    case Word w:
                        idParts.Add(w.ToIdentifier(state.SourceId));
                        break;
                    case Asterisk:
                        endsWithWildcard = true;
                        break;
                    default:
                        throw ParserState.ExpectedCategoryException("identifier | wildcard", state.PeekNth(-1));
                }
            }

            if (endsWithWildcard)
            {
                return new QualifiedWildcard(new ObjectName(idParts));
            }

            if (state.ConsumeTokenIs<ParenOpen>())
            {
                state.Rewind();
                return ParseFunction(state, new ObjectName(idParts));
            }

            return new CompoundIdentifier(idParts);
        }

        // TODO: Skipped Stuff Here

        if (word.QuoteStyle == QuoteStyle.None && _p.AllowsFunctionCallWithoutParentheses(word.Value))
        {
            return new FunctionCall(new ObjectName(new[] { word.ToIdentifier(state.SourceId) }))
            {
                Arguments = FunctionArguments.EmptyList()
            };
        }
        return new SingleIdentifier(word.ToIdentifier(state.SourceId));
    }

    private FunctionCall ParseFunction(ParserState state, ObjectName name)
    {
        state.ExpectLeftParen();

        FunctionArguments args = ParseFunctionArgumentList(state);

        if (name.Values is [{ QuoteStyle: QuoteStyle.None }] && _p.IsBuiltInFunction(name.Values[0].Name))
        {
            name.Values[0] = new Identifier(name.Values[0].Name.ToUpperInvariant());
        }

        WindowSpec? over = ParseOptionalOver(state);

        return new FunctionCall(name)
        {
            Arguments = args,
            Over = over
        };
    }

    /// <summary>
    /// Parses an optional <c>OVER (...)</c> window specification following a
    /// function call. Returns <c>null</c> if the next token is not <c>OVER</c>.
    /// Inline window specs only — named windows (e.g. <c>OVER w</c>) are not
    /// yet supported.
    /// </summary>
    private WindowSpec? ParseOptionalOver(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.OVER))
        {
            return null;
        }

        state.ExpectLeftParen();

        SqlValueList<Expression>? partitionBy = null;
        if (state.ParseKeywordsAll(Keyword.PARTITION, Keyword.BY))
        {
            partitionBy = state.ParseCommaSeparated(ParseExpr);
        }

        SqlValueList<OrderBy>? orderBy = null;
        if (state.ParseKeywordsAll(Keyword.ORDER, Keyword.BY))
        {
            orderBy = state.ParseCommaSeparated(_p.ComponentParser.ParseOrderBy);
        }

        state.ExpectRightParen();

        return new WindowSpec
        {
            PartitionBy = partitionBy,
            OrderBy = orderBy,
        };
    }

    private FunctionArguments.List ParseFunctionArgumentList(ParserState state)
    {
        if (state.ConsumeTokenIs<ParenClose>())
        {
            return FunctionArguments.EmptyList();
        }

        DuplicateTreatment? duplicateTreatment = ComponentParser.ParseOptionalDuplicateTreatment(state);

        SqlValueList<FunctionArgument> args = state.ParseCommaSeparated(ParseFunctionArgument);
        SqlValueList<FunctionArgumentClause>? clauses = null;

        if (state.ParseKeywordsAll(Keyword.ORDER, Keyword.BY))
        {
            clauses ??= [];
            clauses.Add(new FunctionArgumentClause.OrderBy(state.ParseCommaSeparated(_p.ComponentParser.ParseOrderBy)));
        }

        if (state.ParseKeyword(Keyword.LIMIT))
        {
            clauses ??= [];
            clauses.Add(new FunctionArgumentClause.Limit(ParseExpr(state)));
        }

        if (state.ParseKeyword(Keyword.SEPARATOR))
        {
            clauses ??= [];
            clauses.Add(new FunctionArgumentClause.Separator(ValueParser.ParseValue(state)));
        }

        state.ExpectRightParen();

        return new FunctionArguments.List(args, duplicateTreatment, clauses);
    }

    private FunctionArgument ParseFunctionArgument(ParserState state)
    {
        FunctionArgumentOperator? op = state.PeekNth(1) switch
        {
            Equal => FunctionArgumentOperator.EqualSign,
            FatArrow => FunctionArgumentOperator.FatArrow,
            Walrus => FunctionArgumentOperator.Assignment,
            _ => null
        };

        if (op != null)
        {
            Identifier name = _parseIdentifier(state);
            state.Next(); // consume operator
            Expression wildcardOrExpr = ParseWildcardOrExpr(state);
            FunctionArgumentExpression val = ExpressionToArgument(wildcardOrExpr);
            return new FunctionArgument.Named(name, val, op);
        }

        return new FunctionArgument.Unnamed(ExpressionToArgument(ParseWildcardOrExpr(state)));

        static FunctionArgumentExpression ExpressionToArgument(Expression expr)
        {
            return expr switch
            {
                QualifiedWildcard qw => new FunctionArgumentExpression.QualifiedWildcard(qw.Qualifier),
                Wildcard => new FunctionArgumentExpression.Wildcard(),
                _ => new FunctionArgumentExpression.FunctionExpression(expr)
            };
        }
    }

    private Case ParseCaseExpr(ParserState state)
    {
        state.ExpectKeyword(Keyword.CASE);

        Expression? operand = null;
        if (!state.ParseKeyword(Keyword.WHEN))
        {
            operand = ParseExpr(state);
            state.ExpectKeyword(Keyword.WHEN);
        }

        var conditions = new SqlValueList<Expression>();
        var results = new SqlValueList<Expression>();

        while (true)
        {
            conditions.Add(ParseExpr(state));
            state.ExpectKeyword(Keyword.THEN);
            results.Add(ParseExpr(state));
            if (!state.ParseKeyword(Keyword.WHEN))
            {
                break;
            }
        }

        Expression? elseResult = state.ParseInit(state.ParseKeyword(Keyword.ELSE), ParseExpr);
        state.ExpectKeyword(Keyword.END);

        return new Case(conditions, results)
        {
            Operand = operand,
            ElseResult = elseResult
        };
    }

    private Expressions.Convert ParseConvert(ParserState state)
    {
        state.ExpectKeyword(Keyword.CONVERT);

        state.ExpectLeftParen();

        Expression expr = ParseExpr(state);

        if (state.ParseKeyword(Keyword.USING))
        {
            string characterSet = ValueParser.ParseLiteralString(state);
            state.ExpectRightParen();
            return new Expressions.Convert.UsingCharset(expr, characterSet);
        }
        else
        {
            state.ExpectToken<Comma>();
            DataType dataType = _p.DataTypeParser.ParseDataTypeForConvert(state);
            state.ExpectRightParen();
            return new Expressions.Convert.ToDataType(expr, dataType);
        }
    }

    private Cast ParseCast(ParserState state)
    {
        state.ExpectKeyword(Keyword.CAST);

        state.ExpectLeftParen();

        Expression expr = ParseExpr(state);

        state.ExpectKeyword(Keyword.AS);

        DataType dataType = _p.DataTypeParser.ParseDataTypeForConvert(state);

        state.ExpectRightParen();

        return new Cast(expr, dataType);
    }

    private Expression ParseNot(ParserState state)
    {
        state.ExpectKeyword(Keyword.NOT);

        if (state.Peek() is Word { Keyword: Keyword.EXISTS })
        {
            return ParseExists(state, true);
        }
        return new UnaryOperatorExp(ParseSubExpression(state, _pm.GetPrecedence(Precedence.UnaryNot)), UnaryOperatorOp.Not);
    }

    private Expression ParseNotExclamation(ParserState state)
    {
        state.ExpectToken<Exclamation>();

        if (state.PeekKeyword(Keyword.EXISTS))
        {
            return ParseExists(state, true);
        }
        return new UnaryOperatorExp(ParseSubExpression(state, _pm.GetPrecedence(Precedence.UnaryNot)), UnaryOperatorOp.Not);
    }

    public static Expression ParseValueExpr(ParserState state)
    {
        return new LiteralValue(ValueParser.ParseValue(state));
    }

    private UnaryOperatorExp ParseUnaryExpr(ParserState state)
    {
        Token token = state.Next();
        try
        {
            UnaryOperatorOp op = token is Plus ? UnaryOperatorOp.Plus : UnaryOperatorOp.Minus;
            return new UnaryOperatorExp(
                ParseSubExpression(state, _pm.GetPrecedence(Precedence.MultiplyDivide)),
                op
            );
        }
        catch (ParseException)
        {
            throw state.ExpectedCategoryException("variable value");
        }
    }

    private Expression ParseIn(ParserState state, Expression expr, bool negated)
    {
        Expression inOp = state.ParseParenthesized<Expression>((innerState) =>
        {
            if (!innerState.ParseKeyword(Keyword.SELECT) && !innerState.ParseKeyword(Keyword.WITH))
            {
                return new InList(expr, innerState.ParseCommaSeparated(ParseExpr), negated);
            }
            innerState.Rewind();
            return new InSubquery(_p.SelectParser.ParseSimpleSelect(innerState), negated, expr);
        });

        return inOp;
    }

    private Expression ParseExists(ParserState state, bool negated)
    {
        state.ExpectKeyword(Keyword.EXISTS);
        return state.ParseParenthesized<Expression>((innerState) => new Exists(_p.SelectParser.ParseSelect(innerState, null), negated));
    }

    private Like ParseLike(ParserState state, Expression expr, bool negated)
    {
        bool any = state.ParseKeyword(Keyword.ANY);
        Expression sub = ParseSubExpression(state, _pm.GetPrecedence(Precedence.Like));
        string? escapeChar = ParseOptionalEscapeChar(state);

        return new Like(expr, negated, sub, escapeChar, any);
    }

    private static string? ParseOptionalEscapeChar(ParserState state)
    {
        if (state.ParseKeyword(Keyword.ESCAPE))
        {
            return ValueParser.ParseLiteralString(state);
        }
        return null;
    }

    private Between ParseBetween(ParserState state, Expression expr, bool negated)
    {
        short prec = _pm.GetPrecedence(Precedence.Between);
        Expression low = ParseSubExpression(state, prec);
        state.ExpectKeyword(Keyword.AND);
        Expression high = ParseSubExpression(state, prec);
        return new Between(expr, low, high, negated);
    }

    public Expression ParseWildcardOrExpr(ParserState state)
    {
        Token next = state.Peek();
        state.SavePosition();

        if (next is Word w && state.PeekNthIs<Dot>(1))
        {
            var ident = w.ToIdentifier(state.SourceId);
            state.Next();

            var idParts = new SqlValueList<Identifier>() { ident };
            while (state.ConsumeTokenIs<Dot>())
            {
                next = state.Next();
                switch (next)
                {
                    case Word wSub:
                        idParts.Add(wSub.ToIdentifier(state.SourceId));
                        break;
                    case Asterisk:
                        state.ClearSavedPosition();
                        return new QualifiedWildcard(new ObjectName(idParts));
                    default:
                        state.ClearSavedPosition();
                        throw ParserState.ExpectedCategoryException("identifier or .*", next);
                }
            }
        }
        else if (next is Asterisk)
        {
            state.Next();
            state.ClearSavedPosition();
            return new Wildcard();
        }
        state.RewindToSaved();
        return ParseExpr(state);
    }

    private Expression ParseParenOpen(ParserState state)
    {
        state.ExpectLeftParen();

        Expression expr;

        Expression? subquery = TryParseExpressionSubquery(state);
        if (subquery != null)
        {
            expr = subquery;
        }
        // TODO TryParseLambda
        else
        {
            SqlValueList<Expression> expressions = state.ParseCommaSeparated(ParseExpr);
            expr = expressions.Count switch
            {
                0 => throw state.ExpectedCategoryException("comma-separated list"),
                1 => new Nested(expressions.First()),
                _ => new Expressions.Tuple(expressions)
            };
        }

        state.ExpectRightParen();

        // TODO Consider CompositeAccess

        return expr;
    }

    private Expression ParseAfterIs(ParserState state, Expression expr)
    {
        bool negated = state.ParseKeyword(Keyword.NOT);

        if (state.ParseKeyword(Keyword.NULL))
        {
            return new IsNull(expr, negated);
        }
        if (state.ParseKeyword(Keyword.TRUE))
        {
            return new IsTrue(expr, negated);
        }
        if (state.ParseKeyword(Keyword.FALSE))
        {
            return new IsFalse(expr, negated);
        }
        if (state.ParseKeyword(Keyword.UNKNOWN))
        {
            return new IsUnknown(expr, negated);
        }
        if (state.ParseKeywordsAll(Keyword.DISTINCT, Keyword.FROM))
        {
            return new IsDistinctFrom(expr, ParseExpr(state), negated);
        }
        throw state.ExpectedException("[NOT] NULL", "[NOT] TRUE", "[NOT] FALSE", "[NOT] DISTINCT FROM");
    }

    private Subquery? TryParseExpressionSubquery(ParserState state)
    {
        Token next = state.Peek();
        if (next is Word { Keyword: Keyword.SELECT })
        {
            return new Subquery(_p.SelectParser.ParseSelect(state, null));
        }
        // TODO: Consider parsing CTE query here?
        return null;
    }

    private Interval ParseInterval(ParserState state)
    {
        state.ExpectKeyword(Keyword.INTERVAL);
        Expression value = ParsePrefix(state);
        DateTimeUnit unit = state.Peek().ToDateTimeUnit() ?? throw state.ExpectedCategoryException("date_time unit");
        state.Next();
        return new Interval(value, unit);
    }

    private Position ParsePositionIn(ParserState state)
    {
        state.ExpectKeyword(Keyword.POSITION);
        state.ExpectLeftParen();
        Expression subExpr = ParseSubExpression(state, _pm.GetPrecedence(Precedence.Between));
        state.ExpectKeyword(Keyword.IN);
        Expression inExpr = ParseExpr(state);
        state.ExpectRightParen();
        return new Position(subExpr, inExpr);
    }

    private Expression ParsePositionInOrFunction(ParserState state)
    {
        if (state.TryParse(ParsePositionIn, out Position? position))
        {
            return position;
        }
        Word initialAsWord = (state.Next() as Word) ?? throw ParserState.ExpectedException("POSITION", state.Peek());

        return ParseFunction(state, new ObjectName([new Identifier(initialAsWord.Value)]));
    }
}
