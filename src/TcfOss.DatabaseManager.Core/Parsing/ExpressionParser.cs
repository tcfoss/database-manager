using System.Diagnostics;
using System.Globalization;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
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

    protected virtual bool UsesRestrictedBetweenBounds => false;
    protected virtual bool SupportsLegacyPatternAny => true;

    public ExpressionParser(Parser parser)
    {
        _p = parser;
        _pm = _p.PrecedenceManager;

        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
    }


    public Expression ParseExpr(ParserState state)
    {
        using ParserState.Scope scope = state.EnterContext(disable: ParseContext.DefaultExpression | ParseContext.BetweenBound);
        return ParseSubExpression(state, 0);
    }

    public Expression ParseDefaultExpr(ParserState state)
    {
        using ParserState.Scope scope = state.EnterContext(ParseContext.DefaultExpression);
        return ParseSubExpression(state, 0);
    }

    protected Expression ParseSubExpression(ParserState state, short precedence)
    {
        Expression expr = ParsePrefix(state);
        PrecedenceManager.Binding previousBinding = default;

        while (true)
        {
            if (state.Context.HasFlag(ParseContext.DefaultExpression) && state.PeekKeyword(Keyword.COLLATE))
            {
                break;
            }
            PrecedenceManager.Binding nextBinding = _pm.GetNextBinding(state);
            short nextPrecedence = nextBinding.Precedence;
            if (precedence >= nextPrecedence)
            {
                break;
            }
            if (previousBinding.Precedence == nextPrecedence && nextBinding.IsNonAssociative)
            {
                throw state.ExpectedException("parenthesized expression");
            }
            expr = ParseInfix(state, expr, nextPrecedence);
            previousBinding = _pm.GetCompletedBinding(expr, nextBinding);
        }

        return expr;
    }

    protected virtual Expression ParsePrefix(ParserState state)
    {
        if (state.Context.HasFlag(ParseContext.BetweenBound) && state.PeekKeyword(Keyword.NOT))
        {
            throw state.ExpectedException("expression");
        }
        if (!state.PeekKeyword(Keyword.INTERVAL) && _p.DataTypeParser.IsDataTypeStart(state.Peek())
            && state.TryParse((s) =>
        {
            DataType dataType = _p.DataTypeParser.ParseDataType(s);
            return dataType switch
            {
                // TODO: Handle Custom
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
            Word { Keyword: Keyword.ARRAY } when _p.SupportsPostgresArrayAndRowExpressionSyntax && state.PeekNth(1) is BracketOpen or ParenOpen => ParseArrayConstructor(state),
            Word { Keyword: Keyword.ROW } when _p.SupportsPostgresArrayAndRowExpressionSyntax && state.PeekNthIs<ParenOpen>(1) => ParseRowConstructor(state),
            Word { Keyword: Keyword.OPERATOR } => ParsePostgresUnaryOperator(state),
            Word { Keyword: Keyword.CONVERT } => ParseConvert(state),
            Word { Keyword: Keyword.CAST } => ParseCast(state),
            Word { Keyword: Keyword.VALUE } when state.Context.HasFlag(ParseContext.DomainCheck) => ParseDomainValue(state),
            PositionalParameterToken => ParsePositionalParameter(state),
            Minus or Plus => ParseUnaryExpr(state),
            OperatorToken
                or Tilde
                or TildeAsterisk
                or NotTilde
                or NotTildeAsterisk
                or Pipe
                or Ampersand
                or DoublePipe when _p.SupportsPostgresOperatorSyntax => ParsePostgresUnaryOperator(state),
            NumericLiteral
                or StringLiteral
                or EscapeStringLiteral
                or UnicodeStringLiteral
                or BitStringLiteral
                or DollarQuotedStringLiteral
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

        if (token is BracketOpen)
        {
            if (!_p.SupportsPostgresArrayAndRowExpressionSyntax
                || left is not (SingleIdentifier or CompoundIdentifier or PositionalParameter or Nested or Subquery or CompositeAccess.Field))
            {
                throw ParserState.ExpectedException("expression operator", token);
            }
            state.Rewind();
            SqlValueList<ArraySubscriptDimension> dimensions = [];
            do
            {
                dimensions.Add(ParseArraySubscript(state));
            } while (state.PeekIs<BracketOpen>());
            return ParseCompositeContinuation(state, new ArraySubscript(left, dimensions));
        }

        if (token is DoubleColon)
        {
            if (!_p.SupportsPostgresCastSyntax)
            {
                throw ParserState.ExpectedException("expression operator", token);
            }
            return new Cast(left, _p.DataTypeParser.ParseDataTypeReference(state))
            {
                UsePostgresCastSyntax = true
            };
        }

        BinaryOperatorOp? op = GetBinaryOperator(token);
        if (op != null)
        {
            return ParseInfixBinaryOperator(state, token, op, left, precedence);
        }

        if (token is Word word && word.Keyword != Keyword.OPERATOR)
        {
            return ParseInfixWord(state, word, left, precedence);
        }

        if (IsGenericOperatorToken(token))
        {
            return ParseInfixPostgresOperator(state, token, left, precedence);
        }

        throw ParserState.ExpectedException("expression operator", token);
    }

    private Expression ParseInfixBinaryOperator(ParserState state, Token token, BinaryOperatorOp op, Expression left, short precedence)
    {
        Keyword qualifierKeyword = state.ParseKeywordsAny(Keyword.ANY, Keyword.ALL, Keyword.SOME);
        if (qualifierKeyword != Keyword.undefined)
        {
            Expression right = ParseQuantifiedOperand(state, precedence);

            if (!SupportsQuantifiedOperator(op))
            {
                throw ParserState.ExpectedCategoryException("comparison_operator", token);
            }

            // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
            return qualifierKeyword switch
            {
                Keyword.ALL => new QuantifiedBinaryOperator(left, op, right, AggregateQuantifier.All),
                Keyword.ANY => new QuantifiedBinaryOperator(left, op, right, AggregateQuantifier.Any),
                Keyword.SOME => new QuantifiedBinaryOperator(left, op, right, AggregateQuantifier.Some),
                _ => throw new DefensiveGuardException("comparison quantifier keyword", qualifierKeyword.ToString())
            };
        }

        return new BinaryOperatorExp(left, op, ParseSubExpression(state, precedence));
    }

    protected virtual bool SupportsQuantifiedOperator(BinaryOperatorOp op)
    {
        return op != BinaryOperatorOp.And && op != BinaryOperatorOp.Or && op != BinaryOperatorOp.Xor;
    }

    protected static bool IsComparisonOperator(BinaryOperatorOp op)
    {
        return op == BinaryOperatorOp.GreaterThan || op == BinaryOperatorOp.GreaterThanOrEqual
            || op == BinaryOperatorOp.LessThan || op == BinaryOperatorOp.LessThanOrEqual
            || op == BinaryOperatorOp.Equal || op == BinaryOperatorOp.NotEqual;
    }

    protected virtual bool IsGenericOperatorToken(Token token)
    {
        return token is OperatorToken or Word { Keyword: Keyword.OPERATOR };
    }

    protected virtual Expression ParseQuantifiedOperand(ParserState state, short precedence)
    {
        return ParseCompleteQuantifiedOperand(state);
    }

    protected Expression ParseLegacyQuantifiedOperand(ParserState state, short precedence)
    {
        state.ExpectLeftParen();
        if (PeekSubquery(state))
        {
            state.Rewind();
            return ParseSubExpression(state, precedence);
        }
        Expression right = ParseSubExpression(state, precedence);
        state.ExpectRightParen();
        return right;
    }

    private PgUnaryOperator ParsePostgresUnaryOperator(ParserState state)
    {
        PgOperatorReference reference = ParsePostgresOperatorReference(state);
        return new PgUnaryOperator(reference, ParseSubExpression(state, _pm.GetPrecedence(Precedence.PgOperator)));
    }

    private PgBinaryOperator ParseInfixPostgresOperator(ParserState state, Token token, Expression left, short precedence)
    {
        if (!_p.SupportsPostgresOperatorSyntax)
        {
            throw ParserState.ExpectedException("expression operator", token);
        }
        state.Rewind();
        PgOperatorReference reference = ParsePostgresOperatorReference(state);
        AggregateQuantifier? quantifier = state.ParseKeywordsAny(Keyword.ANY, Keyword.ALL, Keyword.SOME) switch
        {
            Keyword.ANY => AggregateQuantifier.Any,
            Keyword.ALL => AggregateQuantifier.All,
            Keyword.SOME => AggregateQuantifier.Some,
            _ => null
        };
        Expression right;
        if (quantifier != null)
        {
            right = ParseCompleteQuantifiedOperand(state);
        }
        else
        {
            right = ParseSubExpression(state, precedence);
        }
        return new PgBinaryOperator(left, reference, right) { Quantifier = quantifier };
    }

    private Expression ParseCompleteQuantifiedOperand(ParserState state)
    {
        if (UsesRestrictedBetweenBounds && state.Context.HasFlag(ParseContext.BetweenBound))
        {
            throw state.ExpectedException("restricted expression");
        }
        state.ExpectLeftParen();
        Expression right;
        if (PeekSubquery(state) || state.PeekKeyword(Keyword.WITH) || state.PeekKeyword(Keyword.VALUES))
        {
            CommonTableExpression? cte = state.PeekKeyword(Keyword.WITH) ? _p.SelectParser.ParseCommonTableExpression(state) : null;
            right = new Subquery(_p.SelectParser.ParseSelect(state, cte));
        }
        else
        {
            right = ParseExpr(state);
        }
        state.ExpectRightParen();
        return right;
    }

    private PgOperatorReference ParsePostgresOperatorReference(ParserState state)
    {
        if (!_p.SupportsPostgresOperatorSyntax)
        {
            throw state.ExpectedException("expression operator");
        }
        bool useOperatorSyntax = state.ParseKeyword(Keyword.OPERATOR);
        Identifier? schema = null;
        if (useOperatorSyntax)
        {
            state.ExpectLeftParen();
            if (state.PeekIs<Word>())
            {
                schema = _parseIdentifier(state);
                state.ExpectToken<Dot>();
            }
        }
        Token token = state.Next();
        if (!IsGenericOperatorToken(token) && GetBinaryOperator(token) == null)
        {
            throw ParserState.ExpectedException("operator name", token);
        }

        string? name = token.ToString();
        if (!PgOperatorReference.IsValidName(name))
        {
            throw ParserState.ExpectedException("operator name", token);
        }
        if (useOperatorSyntax)
        {
            state.ExpectRightParen();
        }
        return new PgOperatorReference(name) { Schema = schema, UseOperatorSyntax = useOperatorSyntax };
    }

    private Expression ParseInfixWord(ParserState state, Word word, Expression left, short precedence)
    {
        Keyword keyword = word.Keyword;

        if (keyword == Keyword.COLLATE)
        {
            if (!_p.SupportsPostgresExpressionCollationSyntax)
            {
                throw ParserState.ExpectedException("expression operator", word);
            }
            return new Collate(left, _p.ComponentParser.ParseObjectName(state));
        }
        if (keyword == Keyword.IS)
        {
            return ParseAfterIs(state, left);
        }
        if (keyword is Keyword.ISNULL or Keyword.NOTNULL)
        {
            return new IsNull(left, keyword == Keyword.NOTNULL);
        }
        if (keyword == Keyword.AT)
        {
            if (_p.SupportsPostgresOperatorSyntax && state.ParseKeyword(Keyword.LOCAL))
            {
                return new AtLocal(left);
            }
            state.ExpectKeywordsAll(Keyword.TIME, Keyword.ZONE);
            return new AtTimeZone(left, ParseSubExpression(state, precedence));
        }

        bool negated = false;
        if (keyword == Keyword.NOT)
        {
            negated = true;
        }
        else
        {
            state.Rewind();
        }

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
        if (state.ParseKeyword(Keyword.ILIKE))
        {
            (Expression? pattern, PatternEscape? escape, bool any, AggregateQuantifier? quantifier) = ParsePatternMatch(state);
            return new ILike(left, negated, pattern, escape, any) { Quantifier = quantifier };
        }
        if (state.ParseKeyword(Keyword.SIMILAR))
        {
            state.ExpectKeyword(Keyword.TO);
            (Expression? pattern, PatternEscape? escape, bool any, AggregateQuantifier? _) = ParsePatternMatch(state, false);
            return new SimilarTo(left, negated, pattern, escape, any);
        }

        throw new DefensiveGuardException("infix keyword", keyword.ToString(), "PrecedenceManager.GetNextPrecedence call in ParseSubExpression will prevent us from getting here.");
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

    private Expression ParsePositionalParameter(ParserState state)
    {
        var token = (PositionalParameterToken)state.Next();
        if (!int.TryParse(token.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
        {
            throw ParserState.ExpectedCategoryException("positional_parameter", token);
        }
        var parameter = new PositionalParameter(number)
        {
            Source = new SourceRef(state.SourceId, token.Location.Position, token.Location.Position + token.Length)
        };
        return ParseCompositeContinuation(state, parameter);
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
            string characterSet = ValueParser.ParseLiteralStringOptionalQuotes(state);
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

        DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReferenceForConvert(state);

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
                ParseSubExpression(state, _pm.GetPrecedence(Precedence.UnarySign)),
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
        (Expression? pattern, PatternEscape? escape, bool any, AggregateQuantifier? quantifier) = ParsePatternMatch(state);
        return new Like(expr, negated, pattern, escape, any) { Quantifier = quantifier };
    }

    private (Expression Pattern, PatternEscape? Escape, bool Any, AggregateQuantifier? Quantifier) ParsePatternMatch(ParserState state, bool allowQuantifier = true)
    {
        if (_p.SupportsPostgresOperatorSyntax && (!SupportsLegacyPatternAny || !state.PeekKeyword(Keyword.ANY)))
        {
            AggregateQuantifier? quantifier = state.ParseKeywordsAny(Keyword.ANY, Keyword.ALL, Keyword.SOME) switch
            {
                Keyword.ANY => AggregateQuantifier.Any,
                Keyword.ALL => AggregateQuantifier.All,
                Keyword.SOME => AggregateQuantifier.Some,
                _ => null
            };
            if (quantifier != null)
            {
                if (!allowQuantifier)
                {
                    throw state.ExpectedException("pattern expression");
                }
                return (ParseCompleteQuantifiedOperand(state), null, false, quantifier);
            }
            Expression postgresPattern = ParseSubExpression(state, _pm.GetPrecedence(Precedence.Like));
            PatternEscape? escape = null;
            if (state.ParseKeyword(Keyword.ESCAPE))
            {
                escape = ParsePatternEscape(state);
            }
            return (postgresPattern, escape, false, null);
        }
        bool any = state.ParseKeyword(Keyword.ANY);
        Expression pattern = ParseSubExpression(state, _pm.GetPrecedence(Precedence.Like));
        PatternEscape? characterEscape = ParseOptionalCharacterEscape(state);

        return (pattern, characterEscape, any, null);
    }

    protected virtual PatternEscape ParsePatternEscape(ParserState state)
    {
        if (state.PeekIs<StringLiteral>())
        {
            return new PatternEscape.Character(ValueParser.ParseLiteralString(state));
        }
        return new PatternEscape.Expression(ParseSubExpression(state, _pm.GetPrecedence(Precedence.Like)));
    }

    private static PatternEscape.Character? ParseOptionalCharacterEscape(ParserState state)
    {
        if (state.ParseKeyword(Keyword.ESCAPE))
        {
            return new PatternEscape.Character(ValueParser.ParseLiteralString(state));
        }
        return null;
    }

    private Between ParseBetween(ParserState state, Expression expr, bool negated)
    {
        bool? symmetric = null;
        if (_p.SupportsPostgresOperatorSyntax)
        {
            symmetric = state.ParseKeywordsAny(Keyword.SYMMETRIC, Keyword.ASYMMETRIC) switch
            {
                Keyword.SYMMETRIC => true,
                Keyword.ASYMMETRIC => false,
                _ => null
            };
        }
        short prec = _pm.GetPrecedence(Precedence.Between);
        Expression low;
        if (UsesRestrictedBetweenBounds)
        {
            using ParserState.Scope scope = state.EnterContext(ParseContext.BetweenBound);
            low = ParseSubExpression(state, 0);
        }
        else
        {
            low = ParseSubExpression(state, prec);
        }
        state.ExpectKeyword(Keyword.AND);
        Expression high = ParseSubExpression(state, prec);
        return new Between(expr, low, high, negated) { Symmetric = symmetric };
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
                // Result cannot be 0 (ParseExpr sees closing paren and expects an expression)
                1 => new Nested(expressions.First()),
                _ => new Expressions.Tuple(expressions)
            };
        }

        state.ExpectRightParen();

        return ParseCompositeContinuation(state, expr);
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
            return new IsDistinctFrom(expr, ParseSubExpression(state, _pm.GetPrecedence(Precedence.Is)), negated);
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


    private Expression ParseArrayConstructor(ParserState state)
    {
        state.ExpectKeyword(Keyword.ARRAY);
        if (state.PeekIs<BracketOpen>())
        {
            return ParseArrayElements(state, useArrayKeyword: true);
        }

        state.ExpectLeftParen();
        CommonTableExpression? cte = null;
        if (state.PeekKeyword(Keyword.WITH))
        {
            cte = _p.SelectParser.ParseCommonTableExpression(state);
        }
        Select query = _p.SelectParser.ParseSelect(state, cte);
        state.ExpectRightParen();
        return new ArrayConstructor.Subquery(query);
    }

    private ArrayConstructor.Elements ParseArrayElements(ParserState state, bool useArrayKeyword)
    {
        state.ExpectToken<BracketOpen>();
        SqlValueList<Expression> values = [];
        if (!state.PeekIs<BracketClose>())
        {
            values = state.PeekIs<BracketOpen>()
                ? [.. state.ParseCommaSeparated(innerState => ParseArrayElements(innerState, useArrayKeyword: false))]
                : state.ParseCommaSeparated(ParseExpr);
        }
        state.ExpectToken<BracketClose>();
        return new ArrayConstructor.Elements(values) { UseArrayKeyword = useArrayKeyword };
    }

    private ArraySubscriptDimension ParseArraySubscript(ParserState state)
    {
        state.ExpectToken<BracketOpen>();
        Expression? lowerBound = null;
        if (!state.PeekIs<Colon>() && !state.PeekIs<BracketClose>())
        {
            lowerBound = ParseExpr(state);
        }

        ArraySubscriptDimension part;
        if (state.ConsumeTokenIs<Colon>())
        {
            Expression? upperBound = state.PeekIs<BracketClose>() ? null : ParseExpr(state);
            part = new ArraySubscriptDimension.Slice(lowerBound, upperBound);
        }
        else if (lowerBound != null)
        {
            part = new ArraySubscriptDimension.Index(lowerBound);
        }
        else
        {
            throw state.ExpectedException("array subscript");
        }

        state.ExpectToken<BracketClose>();
        return part;
    }

    private RowConstructor ParseRowConstructor(ParserState state)
    {
        state.ExpectKeyword(Keyword.ROW);
        SqlValueList<Expression> fields = state.ParseParenthesizedCommaSeparated(ParseExpr, allowEmpty: true);
        return new RowConstructor(fields);
    }

    private Expression ParseCompositeContinuation(ParserState state, Expression expression)
    {
        if (!_p.SupportsPostgresArrayAndRowExpressionSyntax)
        {
            return expression;
        }

        while (state.ConsumeTokenIs<Dot>())
        {
            if (state.ConsumeTokenIs<Asterisk>())
            {
                return new CompositeAccess.Wildcard(expression);
            }
            expression = new CompositeAccess.Field(expression, _parseIdentifier(state));
        }
        return expression;
    }

    private Expression ParseInterval(ParserState state)
    {
        state.ExpectKeyword(Keyword.INTERVAL);

        if (_p.SupportsPostgresIntervalLiteralSyntax
            && (!_p.SupportsMySqlIntervalExpressionSyntax
                || IsIntervalString(state.Peek())
                    && (state.PeekNth(1).ToDateTimeUnit() == null
                        || state.PeekNth(2) is Word { Keyword: Keyword.TO } or ParenOpen)
                || state.Peek() is ParenOpen && IsIntervalString(state.PeekNth(3))))
        {
            uint? precision = DataTypeParser.ParseIntervalPrecision(state);
            if (!IsIntervalString(state.Peek()))
            {
                throw state.ExpectedException("string literal");
            }
            Value literal = ValueParser.ParseValue(state);
            PgIntervalQualifier? qualifier = precision == null ? DataTypeParser.ParsePostgresIntervalQualifier(state) : null;
            return new PgIntervalLiteral(literal, precision, qualifier);
        }

        Expression value = ParsePrefix(state);
        DateTimeUnit unit = state.Peek().ToDateTimeUnit() ?? throw state.ExpectedCategoryException("date_time unit");
        state.Next();
        return new Interval(value, unit);
    }

    private static bool IsIntervalString(Token token)
    {
        return token is StringLiteral or EscapeStringLiteral or UnicodeStringLiteral or DollarQuotedStringLiteral;
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
        Word initialAsWord = (Word)state.Next(); // Only enter this function if state.Next() yields POSITION

        return ParseFunction(state, new ObjectName([new Identifier(initialAsWord.Value)]));
    }

    public Expression ParseDomainCheckExpr(ParserState state)
    {
        using ParserState.Scope scope = state.EnterContext(ParseContext.DomainCheck);
        state.ExpectLeftParen();
        Expression expression = ParseExpr(state);
        state.ExpectRightParen();
        return expression;
    }

    private static DomainValue ParseDomainValue(ParserState state)
    {
        state.Next();
        return new DomainValue();
    }

    protected virtual BinaryOperatorOp? GetBinaryOperator(Token token)
    {
        if (token is Tilde or TildeAsterisk or NotTilde or NotTildeAsterisk
            && !_p.SupportsPostgresRegexOperators)
        {
            return null;
        }

        return token switch
        {
            Equal => BinaryOperatorOp.Equal,
            NotEqual => BinaryOperatorOp.NotEqual,
            GreaterThan => BinaryOperatorOp.GreaterThan,
            GreaterThanOrEqual => BinaryOperatorOp.GreaterThanOrEqual,
            LessThan => BinaryOperatorOp.LessThan,
            LessThanOrEqual => BinaryOperatorOp.LessThanOrEqual,
            Plus => BinaryOperatorOp.Add,
            Minus => BinaryOperatorOp.Subtract,
            Asterisk => BinaryOperatorOp.Multiply,
            Slash => BinaryOperatorOp.Divide,
            Modulo => BinaryOperatorOp.Modulo,
            Word { Keyword: Keyword.AND } => BinaryOperatorOp.And,
            Word { Keyword: Keyword.OR } => BinaryOperatorOp.Or,
            Word { Keyword: Keyword.XOR } => BinaryOperatorOp.Xor,
            DoublePipe => BinaryOperatorOp.StringConcat,
            Pipe => BinaryOperatorOp.BitwiseOr,
            Caret => BinaryOperatorOp.BitwiseXor,
            Ampersand => BinaryOperatorOp.BitwiseAnd,
            Tilde => BinaryOperatorOp.RegexMatch,
            TildeAsterisk => BinaryOperatorOp.RegexMatchCaseInsensitive,
            NotTilde => BinaryOperatorOp.RegexNotMatch,
            NotTildeAsterisk => BinaryOperatorOp.RegexNotMatchCaseInsensitive,
            _ => null
        };
    }
}
