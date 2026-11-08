using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgDdlParser(Parser parser) : DdlParser(parser)
{
    protected override bool UseSqlRoutineTail(ParserState state) => true;
    protected override bool SupportsQualifiedTriggerNames => false;

    public override TriggerTime ParseTriggerTime(ParserState state)
    {
        if (state.PeekKeyword(Keyword.FOR))
        {
            throw state.ExpectedException("BEFORE", "AFTER", "INSTEAD OF");
        }
        return base.ParseTriggerTime(state);
    }

    protected override bool ParseOptionalTriggerIfNotExists(ParserState state)
    {
        return false;
    }

    protected override ObjectName? ParseOptionalTriggerOnTableBeforeTime(ParserState state)
    {
        return null;
    }

    protected override TriggerOrder? ParseOptionalTriggerOrder(ParserState state)
    {
        return null;
    }

    protected override bool ParseTriggerBodyStartsWithAs(ParserState state)
    {
        return false;
    }

    protected override List<TriggerEvent>? ParseAdditionalTriggerEventsViaComma(ParserState state)
    {
        return null;
    }

    protected override Statement ParseTriggerBody(ParserState state)
    {
        return ParseTriggerExecute(state);
    }

    protected override RoutineParamsSupportStyle GetRoutineParamsSupportStyle(bool isFunction)
    {
        return isFunction ? RoutineParamsSupportStyle.PostgreSqlFunction : RoutineParamsSupportStyle.PostgreSqlProcedure;
    }
}
