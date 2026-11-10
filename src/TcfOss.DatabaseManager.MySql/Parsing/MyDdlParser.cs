using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyDdlParser(MyParser parser) : DdlParser(parser)
{
    protected override bool SupportsIndexMethodAfterTableName => false;
    protected override bool UseSqlRoutineTail(ParserState state) => false;
    protected override bool SupportsOmittedFunctionReturn => false;
    protected override bool SupportsPostgresTriggerSyntax => false;

    protected override ObjectName? ParseDropIndexOnObject(ParserState state)
    {
        state.ExpectKeyword(Keyword.ON, advance: false);
        return base.ParseDropIndexOnObject(state);
    }

    protected override bool SupportsDropObjectType(DroppableObject objectType)
    {
        return objectType != DroppableObject.MaterializedView;
    }

    protected override RoutineParamsSupportStyle GetRoutineParamsSupportStyle(bool isFunction)
    {
        return isFunction ? RoutineParamsSupportStyle.MySqlFunction : RoutineParamsSupportStyle.MySqlProcedure;
    }

    protected override StatementFunctionReturn ParseFunctionReturn(ParserState state)
    {
        return ParseScalarFunctionReturn(state);
    }

    protected override ObjectName? ParseOptionalTriggerOnTableBeforeTime(ParserState state)
    {
        return null;
    }

    protected override bool ParseTriggerBodyStartsWithAs(ParserState state)
    {
        // In MySQL, the trigger body does not start with AS.
        return false;
    }

    protected override TriggerEvent ParseTriggerEvent(ParserState state)
    {
        return ParseTriggerEventNarrow(state);
    }

    protected override List<TriggerEvent> ParseAdditionalTriggerEvents(ParserState state, out bool eventsUseOr)
    {
        eventsUseOr = false;
        return [];
    }
}
