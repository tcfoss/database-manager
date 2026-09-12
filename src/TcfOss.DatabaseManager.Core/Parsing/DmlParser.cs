using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class DmlParser
{
    private readonly Parser _p;
    private readonly ExpressionParser _expr;
    private readonly ComponentParser _comp;
    private readonly Func<ParserState, ObjectName> _parseObjectName;

    public DmlParser(Parser parser)
    {
        _p = parser;
        _expr = _p.ExpressionParser;
        _comp = _p.ComponentParser;
        _parseObjectName = _p.ComponentParser.ParseObjectName;
    }


    public Insert ParseInsert(ParserState state, CommonTableExpression? cte)
    {
        state.ExpectKeyword(Keyword.INSERT);

        bool into = state.ParseKeyword(Keyword.INTO);

        ObjectName tableName = _parseObjectName(state);

        SqlValueList<Identifier>? columns = null;
        if (state.PeekIs<ParenOpen>())
        {
            columns = _comp.ParseParenthisizedIdentifierList(state, false);
        }

        SqlValueList<SimpleSelectItem>? output = ParseOptionalOutputClause(state);

        Select source = _p.SelectParser.ParseSelect(state, null);

        SqlValueList<Assignment>? onDuplicateKeyUpdate = ParseOptionalOnDuplicateKeyUpdate(state);

        return new Insert(tableName, source)
        {
            CommonTableExpression = cte,
            Into = into,
            Columns = columns,
            Output = output,
            OnDuplicateKeyUpdate = onDuplicateKeyUpdate,
        };
    }

    public Update ParseUpdate(ParserState state, CommonTableExpression? cte)
    {
        state.ExpectKeyword(Keyword.UPDATE);

        TableWithJoins table = _p.SelectParser.ParseTableWithJoins(state);
        state.ExpectKeyword(Keyword.SET);
        SqlValueList<Assignment> assignments = state.ParseCommaSeparated(_comp.ParseAssignment);

        SqlValueList<SimpleSelectItem>? output = ParseOptionalOutputClause(state);

        TableWithJoins? from = state.ParseInit(state.ParseKeyword(Keyword.FROM), _p.SelectParser.ParseTableWithJoins);
        Expression? where = state.ParseInit(state.ParseKeyword(Keyword.WHERE), _expr.ParseExpr);

        return new Update(table, assignments, where)
        {
            From = from,
            CommonTableExpression = cte,
            Output = output,
        };
    }

    public Delete ParseDelete(ParserState state, CommonTableExpression? cte)
    {
        state.ExpectKeyword(Keyword.DELETE);

        SqlValueList<ObjectName>? deleteTables = null;

        if (!state.PeekKeyword(Keyword.FROM))
        {
            deleteTables = state.ParseCommaSeparated(_parseObjectName);
        }

        state.ExpectKeyword(Keyword.FROM);

        SqlValueList<TableWithJoins> from = state.ParseCommaSeparated(_p.SelectParser.ParseTableWithJoins);

        SqlValueList<SimpleSelectItem>? output = ParseOptionalOutputClause(state);

        Expression? where = state.ParseInit(state.ParseKeyword(Keyword.WHERE), _expr.ParseExpr);
        SqlValueList<OrderBy>? orderBy = state.ParseInit(state.ParseKeywordsAll(Keyword.ORDER, Keyword.BY), (s) => s.ParseCommaSeparated(_comp.ParseOrderBy));
        Expression? limit = state.ParseInit(state.ParseKeyword(Keyword.LIMIT), _expr.ParseExpr);
        SqlValueList<SimpleSelectItem>? returning = ParseOptionalReturningClause(state);

        return new Delete(from, where)
        {
            CommonTableExpression = cte,
            DeleteTables = deleteTables,
            Output = output,
            Returning = returning,
            OrderBy = orderBy,
            Limit = limit
        };
    }

    protected virtual SqlValueList<SimpleSelectItem>? ParseOptionalOutputClause(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.OUTPUT))
        {
            return null;
        }
        return state.ParseCommaSeparated(ParseOutputItem);
    }

    private SimpleSelectItem ParseOutputItem(ParserState state)
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

        Identifier? alias = _p.SelectParser.ParseOptionalAlias(state, IsReservedForOutputAlias);

        if (alias != null)
        {
            return new SimpleSelectItem.ExpressionWithAlias(wildcardOrExpr, alias);
        }
        return new SimpleSelectItem.UnnamedExpression(wildcardOrExpr);
    }

    private static bool IsReservedForOutputAlias(Keyword? keyword)
    {
        return keyword switch
        {
            Keyword.VALUES or Keyword.FROM or Keyword.WHERE or Keyword.INTO
                or Keyword.SELECT or Keyword.ORDER or Keyword.OPTION
                or Keyword.GROUP or Keyword.HAVING or Keyword.UNION
                or Keyword.EXCEPT or Keyword.INTERSECT => true,
            _ => KeywordHelper.IsReservedForColumnAlias(keyword),
        };
    }

    protected virtual SqlValueList<Assignment>? ParseOptionalOnDuplicateKeyUpdate(ParserState state)
    {
        if (!state.ParseKeywordsAll(Keyword.ON, Keyword.DUPLICATE, Keyword.KEY, Keyword.UPDATE))
        {
            return null;
        }
        return state.ParseCommaSeparated(_comp.ParseAssignment);
    }

    protected virtual SqlValueList<SimpleSelectItem>? ParseOptionalReturningClause(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.RETURNING))
        {
            return null;
        }
        return state.ParseCommaSeparated(_p.SelectParser.ParseSimpleSelectItem);
    }
}
