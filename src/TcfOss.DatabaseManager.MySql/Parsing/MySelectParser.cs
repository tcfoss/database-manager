using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MySelectParser(Parser parser) : SelectParser(parser)
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
    protected override TableFactor ParseRowsFrom(ParserState state, bool lateral)
    {
        throw state.ExpectedException("table name");
    }

    /// <inheritdoc/>
    protected override bool ParseOptionalWithOrdinality(ParserState state)
    {
        return false;
    }

    /// <inheritdoc/>
    protected override SelectInto ResolveSingleIntoTarget(ParserState state, ObjectName name)
    {
        if (name.Values.Count != 1)
        {
            throw state.ExpectedCategoryException("variable_name");
        }

        return new SelectInto.SelectIntoVariables(name.Values);
    }

    /// <inheritdoc/>
    protected override Top? ParseOptionalTop(ParserState state)
    {
        return null;
    }

    /// <summary>
    /// MySQL does not support T-SQL-style <c>WITH (NOLOCK, ...)</c> table
    /// hints. Index hints in MySQL use a different syntax
    /// (<c>USE INDEX (...)</c>, <c>FORCE INDEX (...)</c>) and are parsed
    /// elsewhere if at all.
    /// </summary>
    protected override SqlValueList<TableHint>? ParseOptionalTableHints(ParserState state)
    {
        return null;
    }
}
