using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsSelectParser(Parser parser) : SelectParser(parser)
{
    /// <inheritdoc/>
    protected override Statement ParseCteBodyQuery(ParserState state)
    {
        Token first = state.Peek();
        if (first is not (Word { Keyword: Keyword.SELECT or Keyword.VALUES or Keyword.WITH } or ParenOpen))
        {
            throw state.ExpectedException("SELECT");
        }
        Statement query = base.ParseCteBodyQuery(state);
        if (query is not Select)
        {
            throw ParserState.ExpectedException("SELECT", first);
        }
        return query;
    }

    /// <inheritdoc/>
    protected override bool? ParseOptionalCteMaterialization(ParserState state)
    {
        return null;
    }

    /// <inheritdoc/>
    protected override PgCteSearch? ParseOptionalCteSearch(ParserState state)
    {
        return null;
    }

    /// <inheritdoc/>
    protected override PgCteCycle? ParseOptionalCteCycle(ParserState state)
    {
        return null;
    }

    /// <inheritdoc/>
    protected override bool ParseOptionalLateral(ParserState state)
    {
        return false;
    }

    /// <inheritdoc/>
    protected override TableFactor ParseRowsFrom(ParserState state, bool lateral)
    {
        throw state.ExpectedException("table name");
    }

    /// <inheritdoc/>
    protected override bool ParseOptionalWithOrdinality(ParserState state)
    {
        return false;
    }

    protected override SelectInto ParseSelectInto(ParserState state)
    {
        SelectInto target = base.ParseSelectInto(state);
        if (target is not SelectInto.SelectIntoTable)
        {
            throw state.ExpectedCategoryException("table_name");
        }

        return target;
    }

    /// <summary>
    /// Wraps the core alias predicate to also treat T-SQL DML continuation
    /// keywords (<c>OUTPUT</c>, <c>OPTION</c>) as reserved.
    /// </summary>
    public override Identifier? ParseOptionalAlias(ParserState state, Func<Keyword, bool> isReserved)
    {
        return base.ParseOptionalAlias(state, k => IsTSqlReserved(k) || isReserved(k));
    }

    private static bool IsTSqlReserved(Keyword? keyword)
    {
        return keyword is Keyword.OUTPUT or Keyword.OPTION;
    }
}
