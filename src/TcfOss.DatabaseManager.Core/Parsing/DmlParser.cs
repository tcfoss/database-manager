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
        Identifier? alias = ParseOptionalInsertAlias(state);

        SqlValueList<Identifier>? columns = null;
        if (state.PeekIs<ParenOpen>())
        {
            columns = _comp.ParseParenthisizedIdentifierList(state, false);
        }

        PgIdentityOverride? overriding = ParseOptionalIdentityOverride(state);
        SqlValueList<SimpleSelectItem>? output = ParseOptionalOutputClause(state);

        Select? source = ParseInsertSource(state);

        SqlValueList<Assignment>? onDuplicateKeyUpdate = ParseOptionalOnDuplicateKeyUpdate(state);
        PgOnConflict? onConflict = ParseOptionalOnConflict(state);
        SqlValueList<SimpleSelectItem>? returning = ParseOptionalReturningClause(state);

        return new Insert(tableName, source)
        {
            CommonTableExpression = cte,
            Into = into,
            Alias = alias,
            Columns = columns,
            Overriding = overriding,
            Output = output,
            OnDuplicateKeyUpdate = onDuplicateKeyUpdate,
            OnConflict = onConflict,
            Returning = returning,
        };
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [ AS alias ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual Identifier? ParseOptionalInsertAlias(ParserState state)
    {
        return state.ParseKeyword(Keyword.AS) ? _comp.ParseIdentifier(state) : null;
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [ OVERRIDING { SYSTEM | USER } VALUE ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual PgIdentityOverride? ParseOptionalIdentityOverride(ParserState state)
    {
        if (!state.ParseKeyword(Keyword.OVERRIDING))
        {
            return null;
        }
        Keyword kind = state.ParseKeywordsAny(Keyword.SYSTEM, Keyword.USER);
        if (kind == Keyword.undefined)
        {
            throw state.ExpectedException("SYSTEM", "USER");
        }
        state.ExpectKeyword(Keyword.VALUE);
        return kind == Keyword.SYSTEM ? PgIdentityOverride.SystemValue : PgIdentityOverride.UserValue;
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// DEFAULT VALUES | VALUES ( expression [, ...] ) [, ...] | select_statement
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual Select? ParseInsertSource(ParserState state)
    {
        if (state.ParseKeywordsAll(Keyword.DEFAULT, Keyword.VALUES))
        {
            return null;
        }
        return _p.SelectParser.ParseSelect(state, null);
    }

    public Update ParseUpdate(ParserState state, CommonTableExpression? cte)
    {
        state.ExpectKeyword(Keyword.UPDATE);

        TableWithJoins table = _p.SelectParser.ParseTableWithJoins(state);
        state.ExpectKeyword(Keyword.SET);
        SqlValueList<Assignment> assignments = state.ParseCommaSeparated(_comp.ParseAssignment);

        SqlValueList<SimpleSelectItem>? output = ParseOptionalOutputClause(state);

        SqlValueList<TableWithJoins>? from = ParseOptionalUpdateFrom(state);
        Expression? where = state.ParseInit(state.ParseKeyword(Keyword.WHERE), _expr.ParseExpr);
        SqlValueList<SimpleSelectItem>? returning = ParseOptionalReturningClause(state);

        return new Update(table, assignments, where)
        {
            From = from,
            CommonTableExpression = cte,
            Output = output,
            Returning = returning,
        };
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [ FROM from_item [, ...] ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual SqlValueList<TableWithJoins>? ParseOptionalUpdateFrom(ParserState state)
    {
        return state.ParseKeyword(Keyword.FROM) ? state.ParseCommaSeparated(_p.SelectParser.ParseTableWithJoins) : null;
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

        SqlValueList<TableWithJoins>? usingTables = ParseOptionalDeleteUsing(state);
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
            Using = usingTables,
            OrderBy = orderBy,
            Limit = limit
        };
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [ USING from_item [, ...] ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual SqlValueList<TableWithJoins>? ParseOptionalDeleteUsing(ParserState state)
    {
        return state.ParseKeyword(Keyword.USING) ? state.ParseCommaSeparated(_p.SelectParser.ParseTableWithJoins) : null;
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
        Token firstToken = state.Peek();
        Expression wildcardOrExpr = _expr.ParseWildcardOrExpr(state);
        if (wildcardOrExpr is Wildcard)
        {
            throw ParserState.ExpectedCategoryException("identifier or qualified wildcard", firstToken);
        }
        if (wildcardOrExpr is QualifiedWildcard q)
        {
            return new SimpleSelectItem.QualifiedWildcard(q.Qualifier);
        }

        Identifier? alias = _p.SelectParser.ParseOptionalAlias(state, IsReservedForOutputAlias);

        if (alias != null)
        {
            return new SimpleSelectItem.ExpressionWithAlias(wildcardOrExpr, alias);
        }
        return new SimpleSelectItem.UnnamedExpression(wildcardOrExpr);
    }

    private static bool IsReservedForOutputAlias(Keyword keyword)
    {
        return keyword switch
        {
            Keyword.DEFAULT or Keyword.VALUES or Keyword.FROM or Keyword.WHERE or Keyword.INTO
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

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [ ON CONFLICT [ conflict_target ] DO NOTHING
    ///     | ON CONFLICT conflict_target DO UPDATE SET assignment [, ...] [ WHERE condition ] ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual PgOnConflict? ParseOptionalOnConflict(ParserState state)
    {
        if (!state.ParseKeywordsAll(Keyword.ON, Keyword.CONFLICT))
        {
            return null;
        }
        PgConflictTarget? target = ParseOptionalConflictTarget(state);
        state.ExpectKeyword(Keyword.DO);
        if (state.ParseKeyword(Keyword.NOTHING))
        {
            return new PgOnConflict.DoNothing(target);
        }
        state.ExpectKeyword(Keyword.UPDATE);
        if (target == null)
        {
            throw state.ExpectedException("conflict target");
        }
        state.ExpectKeyword(Keyword.SET);
        SqlValueList<Assignment> assignments = state.ParseCommaSeparated(_comp.ParseAssignment);
        Expression? where = state.ParseKeyword(Keyword.WHERE) ? _expr.ParseExpr(state) : null;
        return new PgOnConflict.DoUpdate(target, assignments, where);
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// [ ( index_element [, ...] ) [ WHERE predicate ]
    ///     | ON CONSTRAINT constraint_name ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual PgConflictTarget? ParseOptionalConflictTarget(ParserState state)
    {
        if (state.PeekIs<ParenOpen>())
        {
            SqlValueList<PgConflictIndexElement> elements = state.ParseParenthesizedCommaSeparated(ParseConflictIndexElement, false);
            Expression? where = state.ParseKeyword(Keyword.WHERE) ? _expr.ParseExpr(state) : null;
            return new PgConflictTarget.IndexInference(elements, where);
        }
        if (state.ParseKeywordsAll(Keyword.ON, Keyword.CONSTRAINT))
        {
            return new PgConflictTarget.Constraint(_comp.ParseIdentifier(state));
        }
        return null;
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// { column_name | ( expression ) } [ COLLATE collation ] [ operator_class [ ( assignment [, ...] ) ] ]
    /// ]]>
    /// </code>
    /// </summary>
    protected virtual PgConflictIndexElement ParseConflictIndexElement(ParserState state)
    {
        Expression expression = _expr.ParseExpr(state);
        ObjectName? operatorClass = state.PeekIs<Word>() ? _parseObjectName(state) : null;
        SqlValueList<Assignment>? parameters = operatorClass != null && state.PeekIs<ParenOpen>()
            ? state.ParseParenthesizedCommaSeparated(_comp.ParseAssignment, false)
            : null;
        return new PgConflictIndexElement(expression, operatorClass) { OperatorClassParameters = parameters };
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
