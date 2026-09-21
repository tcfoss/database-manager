using System.Diagnostics;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using Ksc = TcfOss.DatabaseManager.Core.Parsing.KeywordSearchCondition;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class SelectParser
{
    private readonly Parser _p;
    private readonly ExpressionParser _expr;
    private readonly Func<ParserState, Identifier> _parseIdentifier;
    private readonly Func<ParserState, ObjectName> _parseObjectName;

    public SelectParser(Parser parser)
    {
        _p = parser;
        _expr = _p.ExpressionParser;
        _parseIdentifier = _p.ComponentParser.ParseIdentifier;
        _parseObjectName = _p.ComponentParser.ParseObjectName;
    }


    public CommonTableExpression ParseCommonTableExpression(ParserState state)
    {
        state.ExpectKeyword(Keyword.WITH);
        bool recursive = state.ParseKeyword(Keyword.RECURSIVE);

        SqlValueList<CommonTableExpressionBody> tables = state.ParseCommaSeparated(ParseCommonTableExpressionBody) ?? throw state.ExpectedCategoryException("CTE definition");
        return new CommonTableExpression(tables, recursive);
    }

    private CommonTableExpressionBody ParseCommonTableExpressionBody(ParserState state)
    {
        Identifier name = _p.ComponentParser.ParseIdentifier(state);

        SqlValueList<Identifier>? columns = state.ParseParenthesizedCommaSeparatedOptional(_parseIdentifier, false);

        state.ExpectKeyword(Keyword.AS);

        Select query = state.ParseParenthesized((s) => ParseSelect(s, null));

        Identifier? from = null;
        if (state.ParseKeyword(Keyword.FROM))
        {
            from = _parseIdentifier(state);
        }

        return new CommonTableExpressionBody(name, columns, query, from);
    }

    public Select ParseSelect(ParserState state, CommonTableExpression? cte)
    {
        SelectBody body = ParseSelectBody(state, 0);

        SqlValueList<OrderBy>? orderBy = null;
        if (state.ParseKeywordsAll(Keyword.ORDER, Keyword.BY))
        {
            orderBy = state.ParseCommaSeparated(_p.ComponentParser.ParseOrderBy);
        }

        Limit? limit = null;
        if (state.PeekKeyword(Keyword.LIMIT))
        {
            limit = _p.ComponentParser.ParseLimit(state);
        }
        else if (state.PeekKeyword(Keyword.OFFSET))
        {
            limit = _p.ComponentParser.ParseOffsetFetch(state);
        }


        SelectInto? intoLate = null;
        if (state.PeekKeyword(Keyword.INTO))
        {
            int numSelections = 0;
            if (body is SelectBody.SimpleSelectQuery simpleSelect)
            {
                numSelections = simpleSelect.Query.Selections.Count;
            }
            else if (body is SelectBody.SetOperation { Left: SelectBody.SimpleSelectQuery leftSimple })
            {
                numSelections = leftSimple.Query.Selections.Count;
            }
            intoLate = ParseSelectInto(state, numSelections);
        }

        return new Select(body)
        {
            CommonTableExpression = cte,
            OrderBy = orderBy,
            Limit = limit,
            SelectInto = intoLate
        };
    }

    private SelectInto ParseSelectInto(ParserState state, int selectionCount)
    {
        state.ExpectKeyword(Keyword.INTO);

        if (state.ParseKeyword(Keyword.OUTFILE))
        {
            string outfile = ValueParser.ParseLiteralString(state);
            return new SelectInto.SelectIntoFile(outfile);
        }

        ObjectName name = _parseObjectName(state);
        if (name.Values.Count > 1)
        {
            return new SelectInto.SelectIntoTable(name);
        }

        if (state.PeekIs<Comma>())
        {
            state.ConsumeTokenIs<Comma>();
            SqlValueList<Identifier> variables = state.ParseCommaSeparated(_parseIdentifier);
            return new SelectInto.SelectIntoVariables([name.Values[0], .. variables]);
        }

        if (selectionCount == 1)
        {
            return new SelectInto.SelectIntoVariables(name.Values);
        }

        return new SelectInto.SelectIntoTable(name);
    }

    public SimpleSelect ParseSimpleSelect(ParserState state)
    {
        state.ExpectKeyword(Keyword.SELECT);

        DistinctFilter? distinct = _p.ComponentParser.ParseAllOrDistinct(state);
        Top? top = ParseOptionalTop(state);

        SqlValueList<SimpleSelectItem> selections = state.ParseCommaSeparated(ParseSimpleSelectItemWithMeta);

        SelectInto? intoEarly = null;
        if (state.PeekKeyword(Keyword.INTO))
        {
            intoEarly = ParseSelectInto(state, selections.Count);
        }

        SqlValueList<TableWithJoins>? from = state.ParseInit(state.ParseKeyword(Keyword.FROM), x => x.ParseCommaSeparated(ParseTableWithJoins));

        Expression? where = state.ParseInit(state.ParseKeyword(Keyword.WHERE), _expr.ParseExpr);

        SqlValueList<Expression>? groupBy = null;
        if (state.ParseKeywordsAll(Keyword.GROUP, Keyword.BY))
        {
            groupBy = state.ParseCommaSeparated(_expr.ParseExpr);
        }

        Expression? having = state.ParseInit(state.ParseKeyword(Keyword.HAVING), _expr.ParseExpr);

        return new SimpleSelect(selections)
        {
            Distinct = distinct,
            Top = top,
            From = from,
            Where = where,
            GroupBy = groupBy,
            Having = having,
            SelectInto = intoEarly,
        };
    }

    /// <summary>
    /// Parses the optional T-SQL <c>TOP (n) [PERCENT] [WITH TIES]</c> clause
    /// that appears immediately after <c>SELECT [ALL|DISTINCT]</c>. Returns
    /// <c>null</c> if the next token is not <c>TOP</c>. Both the parenthesized
    /// form <c>TOP (expr)</c> and the legacy unparenthesized literal form
    /// <c>TOP n</c> are accepted. Dialects that do not support <c>TOP</c>
    /// override this hook to return <c>null</c> unconditionally.
    /// </summary>
    protected virtual Top? ParseOptionalTop(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.TOP))
        {
            return null;
        }

        Expression expression;
        if (state.ConsumeTokenIs<ParenOpen>())
        {
            expression = _expr.ParseExpr(state);
            state.ExpectRightParen();
        }
        else
        {
            // Legacy TOP without parens accepts only a literal value (integer
            // or, when used with PERCENT, a numeric constant). Use the
            // single-value parser so we don't greedily consume the rest of
            // the SELECT list (e.g. the '*' would otherwise be parsed as
            // multiplication).
            expression = ExpressionParser.ParseValueExpr(state);
        }

        bool percent = state.ParseKeyword(Keyword.PERCENT);
        bool withTies = state.ParseKeywordsAll(Keyword.WITH, Keyword.TIES);

        return new Top(expression, percent, withTies);
    }

    private SelectBody ParseSelectBody(ParserState state, short precedence)
    {
        SelectBody? body;

        if (state.PeekKeyword(Keyword.SELECT))
        {
            body = new SelectBody.SimpleSelectQuery(ParseSimpleSelect(state));
        }
        else if (state.ConsumeTokenIs<ParenOpen>())
        {
            Select subquery = ParseSelect(state, null);
            body = new SelectBody.SelectQuery(subquery);
            state.ExpectRightParen();
        }
        else if (state.PeekKeyword(Keyword.VALUES))
        {
            body = new SelectBody.ValuesQuery(ParseValues(state, true));
        }
        else
        {
            throw state.ExpectedException("SELECT", "VALUES", "subquery".Italic());
        }

        return ParseRemainingSetOperations(state, body, precedence);
    }

    private SelectBody ParseRemainingSetOperations(ParserState state, SelectBody body, short precedence)
    {
        while (true)
        {
            SetOperator? op = state.Peek() switch
            {
                Word { Keyword: Keyword.UNION } => SetOperator.Union,
                Word { Keyword: Keyword.EXCEPT } => SetOperator.Except,
                Word { Keyword: Keyword.INTERSECT } => SetOperator.Intersect,
                _ => null
            };

            short nextPrecedence = op switch
            {
                var _ when op == SetOperator.Union => 10,
                var _ when op == SetOperator.Except => 10,
                var _ when op == SetOperator.Intersect => 20,
                _ => -1
            };

            if (nextPrecedence == -1 || precedence >= nextPrecedence)
            {
                break;
            }

            state.Next();
            SetQuantifier? quantifier = ComponentParser.ParseSetQuantifier(state);

            body = new SelectBody.SetOperation(body, op!, ParseSelectBody(state, nextPrecedence), quantifier);
        }

        return body;
    }

    public SimpleSelectItem ParseSimpleSelectItem(ParserState state)
    {
        Expression wildcardOrExpr = _expr.ParseWildcardOrExpr(state);
        if (wildcardOrExpr is QualifiedWildcard q)
        {
            return new SimpleSelectItem.QualifiedWildcard(q.Qualifier);
        }
        if (wildcardOrExpr is Wildcard)
        {
            return new SimpleSelectItem.Wildcard();
        }

        Identifier? alias = ParseOptionalAlias(state, KeywordHelper.IsReservedForColumnAlias);

        if (alias != null)
        {
            return new SimpleSelectItem.ExpressionWithAlias(wildcardOrExpr, alias);
        }
        return new SimpleSelectItem.UnnamedExpression(wildcardOrExpr);
    }

    private SimpleSelectItem ParseSimpleSelectItemWithMeta(ParserState state)
    {
        Token token = state.Peek();
        SimpleSelectItem item = ParseSimpleSelectItem(state);
        if (token.PreNonSql.SafeAny())
        {
            item.Meta.PreNonSql = token.PreNonSql!.ToStatementNonSql();
            item.Meta.Start = token.PreNonSql!.First().Location.Position;
        }
        else
        {
            item.Meta.Start = token.Location.Position;
        }

        Token upcoming = state.Peek();
        item.Meta.End = upcoming.Location.Position;
        if (upcoming is Comma or Eof)
        {
            item.Meta.PostNonSql = upcoming.PreNonSql?.ToStatementNonSql();
            item.Meta.End += upcoming.Length;
        }
        return item;
    }

    private Values ParseValues(ParserState state, bool allowEmpty)
    {
        bool rowConstructor = false;
        state.ExpectKeyword(Keyword.VALUES);
        SqlValueList<SqlValueList<Expression>> rows = state.ParseCommaSeparated(s =>
        {
            if (s.ParseKeyword(Keyword.ROW))
            {
                rowConstructor = true;
            }

            s.ExpectLeftParen();

            if (allowEmpty && s.PeekIs<ParenClose>())
            {
                s.Next();
                return [];
            }

            SqlValueList<Expression> row = state.ParseCommaSeparated(_expr.ParseExpr);
            s.ExpectRightParen();
            return row;
        });

        return new Values(rows)
        {
            RowConstructor = rowConstructor
        };
    }

    public TableWithJoins ParseTableWithJoins(ParserState state)
    {
        TableFactor relation = ParseTableFactor(state);

        SqlValueList<Join>? joins = null;

        while (true)
        {
            Join join;
            if (state.ParseKeyword(Keyword.CROSS))
            {
                JoinOperator joinOperator;
                if (state.ParseKeyword(Keyword.JOIN))
                {
                    joinOperator = new JoinOperator.CrossJoin();
                }
                else if (state.ParseKeyword(Keyword.APPLY))
                {
                    joinOperator = new JoinOperator.CrossApply();
                }
                else
                {
                    throw state.ExpectedException("JOIN", "APPLY");
                }

                join = new Join(ParseTableFactor(state), joinOperator);
            }
            else if (state.ParseKeyword(Keyword.OUTER))
            {
                state.ExpectKeyword(Keyword.APPLY);
                join = new Join(ParseTableFactor(state), new JoinOperator.OuterApply());
            }
            else
            {
                Func<JoinConstraint, JoinOperator> joinConstructor;

                bool natural = state.ParseKeyword(Keyword.NATURAL);
                Keyword peeked = Keyword.undefined;

                if (state.Peek() is Word word)
                {
                    peeked = word.Keyword;
                }

                List<Keyword> joinKeywords;
                if (state.ParseKeywordSequence(Ksc.Optional(Keyword.INNER), Keyword.JOIN).Count > 0)
                {
                    joinConstructor = constraint => new JoinOperator.Inner(constraint);
                }
                else if ((joinKeywords = state.ParseKeywordSequence(Ksc.OneOf(Keyword.LEFT, Keyword.RIGHT, Keyword.FULL), Ksc.Optional(Keyword.OUTER), Keyword.JOIN)).Count > 0)
                {
                    joinConstructor = joinKeywords[0] switch
                    {
                        Keyword.LEFT => constraint => new JoinOperator.LeftOuter(constraint),
                        Keyword.RIGHT => constraint => new JoinOperator.RightOuter(constraint),
                        Keyword.FULL => constraint => new JoinOperator.FullOuter(constraint),
                        _ => throw new UnreachableException()
                    };
                }
                else if (peeked == Keyword.OUTER)
                {
                    throw state.ExpectedException("LEFT", "RIGHT", "FULL");
                }
                else if (natural)
                {
                    throw state.ExpectedCategoryException("join_type");
                }
                else
                {
                    break;
                }

                TableFactor relatedTable = ParseTableFactor(state);
                JoinConstraint joinConstraint = ParseJoinConstraint(state, natural);

                join = new Join(relatedTable, joinConstructor(joinConstraint));
            }

            joins ??= [];
            joins.Add(join);
        }


        return new TableWithJoins(relation) { Joins = joins };
    }

    private TableFactor ParseTableFactor(ParserState state)
    {
        if (state.ConsumeTokenIs<ParenOpen>())
        {
            if (MaybeDerivedTableFactorUpcoming(state) && state.TryParse(ParseDerivedTableFactor, out TableFactor? derivedTable))
            {
                return derivedTable;
            }

            TableWithJoins tableAndJoins = ParseTableWithJoins(state);

            if (tableAndJoins.Joins.SafeAny())
            {
                state.ExpectRightParen();
                Identifier? alias = ParseOptionalAlias(state, KeywordHelper.IsReservedForTableAlias);
                return new TableFactor.NestedJoin
                {
                    TableWithJoins = tableAndJoins,
                    Alias = alias
                };
            }

            if (tableAndJoins.Relation is TableFactor.NestedJoin)
            {
                state.ExpectRightParen();
                Identifier? alias = ParseOptionalAlias(state, KeywordHelper.IsReservedForColumnAlias);
                return new TableFactor.NestedJoin
                {
                    TableWithJoins = tableAndJoins,
                    Alias = alias
                };
            }

            throw state.ExpectedException("JOIN");
        }

        // TODO: Parse Values Expression
        // var peekedTokens = state.PeekN(2);
        // if (peekedTokens[0] is Word { Keyword: Keyword.VALUES } && peekedTokens[1] is ParenOpen)
        // {
        //     state.ExpectKeyword(Keyword.VALUES);
        // }

        // TODO: Parse JSON_TABLE

        ObjectName name = _parseObjectName(state);

        // TODO: Parse potential version qualifier

        // TODO: Parse table-valued function
        // Todo Parse ordinality

        Identifier? tableAlias = ParseOptionalAlias(state, KeywordHelper.IsReservedForTableAlias);

        SqlValueList<TableHint>? hints = ParseOptionalTableHints(state);

        TableFactor table = new TableFactor.Table(name)
        {
            Alias = tableAlias,
            Hints = hints,
        };

        // TODO Parse PIVOT/UNPIVOT

        return table;
    }

    private static bool MaybeDerivedTableFactorUpcoming(ParserState state)
    {
        return state.Peek() switch
        {
            Word { Keyword: Keyword.SELECT or Keyword.WITH or Keyword.VALUES } or ParenOpen => true,
            _ => false,
        };
    }

    private TableFactor ParseDerivedTableFactor(ParserState state)
    {
        CommonTableExpression? cte = null;
        if (state.PeekKeyword(Keyword.WITH))
        {
            cte = ParseCommonTableExpression(state);
        }
        Select subQuery = ParseSelect(state, cte);
        state.ExpectRightParen();
        Identifier? alias = ParseOptionalAlias(state, KeywordHelper.IsReservedForTableAlias);
        return new TableFactor.Derived(subQuery) { Alias = alias };
    }

    private JoinConstraint ParseJoinConstraint(ParserState state, bool natural)
    {
        if (natural)
        {
            return new JoinConstraint.Natural();
        }
        if (state.ParseKeyword(Keyword.ON))
        {
            Expression constraint = _expr.ParseExpr(state);
            return new JoinConstraint.On(constraint);
        }
        if (state.ParseKeyword(Keyword.USING))
        {
            SqlValueList<Identifier> columns = state.ParseParenthesizedCommaSeparated(_parseIdentifier, false);
            return new JoinConstraint.Using(columns);
        }
        return new JoinConstraint.None();
    }

    /// <summary>
    /// Parses an optional T-SQL <c>WITH (hint [, ...])</c> table-hint clause
    /// following a base table reference. Each hint is either a bare
    /// keyword/identifier (e.g. <c>NOLOCK</c>, <c>HOLDLOCK</c>) or
    /// <c>INDEX(idx1, idx2, ...)</c>. Returns <c>null</c> when the next
    /// tokens are not a hint clause. Dialects that do not support table
    /// hints override this hook to return <c>null</c> unconditionally.
    /// </summary>
    protected virtual SqlValueList<TableHint>? ParseOptionalTableHints(ParserState state)
    {
        // Hints take the form WITH (...). Peek two tokens ahead so we don't
        // consume a stray WITH that begins a CTE or other clause.
        Token[] peeked = state.PeekN(2);
        if (peeked.Length < 2 || peeked[0] is not Word { Keyword: Keyword.WITH } || peeked[1] is not ParenOpen)
        {
            return null;
        }
        state.ExpectKeyword(Keyword.WITH);
        state.ExpectLeftParen();

        SqlValueList<TableHint> hints = state.ParseCommaSeparated(ParseTableHint);

        state.ExpectRightParen();
        return hints;
    }

    /// <summary>
    /// Parses a single T-SQL table hint: either <c>INDEX(idx1, idx2, ...)</c>
    /// or a bare identifier/keyword hint such as <c>NOLOCK</c>.
    /// </summary>
    private TableHint ParseTableHint(ParserState state)
    {
        if (state.ParseKeyword(Keyword.INDEX))
        {
            state.ExpectLeftParen();
            SqlValueList<Identifier> indexes = state.ParseCommaSeparated(_parseIdentifier);
            state.ExpectRightParen();
            return new MsTableHint.Index(indexes);
        }

        Identifier name = _parseIdentifier(state);
        return new MsTableHint.Simple(name);
    }


    public virtual Identifier? ParseOptionalAlias(ParserState state, Func<Keyword, bool> isReserved)
    {
        bool afterAs = state.ParseKeyword(Keyword.AS);
        Token token = state.Peek();

        if (token is Word w && (afterAs || !isReserved(w.Keyword)))
        {
            state.Next();
            return w.ToIdentifier(state.SourceId);
        }
        if (token is StringLiteral sl)
        {
            state.Next();
            return new Identifier(sl.Value);
        }

        if (afterAs)
        {
            throw ParserState.ExpectedCategoryException("identifier", token);
        }

        return null;
    }
}
