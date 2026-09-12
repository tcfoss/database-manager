using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class ComponentParser(Parser parser)
{
    private readonly Parser _p = parser;

    public Identifier ParseIdentifier(ParserState state)
    {
        Token token = state.Next();
        return token switch
        {
            Word word => word.ToIdentifier(state.SourceId),
            StringLiteral str => new Identifier(str.Value, _p.DefaultQuoteStyle) { Source = new SourceRef(state.SourceId, token.Location.Position, token.Location.Position + token.Length) },
            _ => throw ParserState.ExpectedCategoryException("identifier", token)
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
        var idents = new SqlValueList<Identifier>();
        while (true)
        {
            Identifier ident = ParseIdentifier(state);
            idents.Add(ident);

            if (!state.ConsumeTokenIs<Dot>())
            {
                break;
            }
        }

        return new ObjectName(idents);
    }

    private KeyPart.Column ParseKeyPartColumn(ParserState state)
    {
        Identifier name = ParseIdentifier(state);
        uint? length = null;
        if (state.ConsumeTokenIs<ParenOpen>())
        {
            length = ValueParser.ParseLiteralUInt(state);
            state.ExpectRightParen();
        }
        Direction? dir = ParseDirection(state);

        Token peeked = state.Peek();
        if (peeked is not Comma and not ParenClose)
        {
            throw state.ExpectedException(")", ",");
        }
        return new KeyPart.Column(name, length, dir);
    }

    private KeyPart ParseKeyPartInList(ParserState state)
    {
        if (state.TryParse(ParseKeyPartColumn, out KeyPart.Column? keyPartColumn))
        {
            return keyPartColumn;
        }

        Expression expr = _p.ExpressionParser.ParseExpr(state);
        Direction? dir = ParseDirection(state);

        return new KeyPart.IndexExpression(expr, dir);
    }

    public SqlValueList<Identifier> ParseParenthisizedIdentifierList(ParserState state, bool allowEmpty)
    {
        return state.ParseParenthesizedCommaSeparated(ParseIdentifier, allowEmpty);
    }

    public SqlValueList<KeyPart> ParseParenthesizedKeyPartList(ParserState state, bool allowEmpty)
    {
        return state.ParseParenthesizedCommaSeparated(ParseKeyPartInList, allowEmpty);
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

        return new OrderBy(expr, dir);
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

    public static CreateOrLabel? ParseCreateOrLabel(ParserState state)
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

    public Limit ParseLimit(ParserState state)
    {
        state.ExpectKeyword(Keyword.LIMIT);
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
    /// Parses an ANSI <c>OFFSET n {ROW|ROWS} [FETCH {FIRST|NEXT} m {ROW|ROWS} ONLY]</c>
    /// pagination clause. The <c>OFFSET</c> keyword must be the next token.
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
}
