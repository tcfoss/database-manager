using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

/// <summary>
/// T-SQL <see cref="SelectParser"/>. Core already implements the full T-SQL
/// syntax surface (<c>TOP</c>, <c>WITH (hint, ...)</c> table hints, etc.);
/// this subclass restricts INTO to table targets and widens the alias-reservation
/// predicate so trailing T-SQL DML keywords (<c>OUTPUT</c>, <c>OPTION</c>)
/// are never silently absorbed as an alias.
/// </summary>
public class MsSelectParser(Parser parser) : SelectParser(parser)
{
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
