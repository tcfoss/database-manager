using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyDdlParser(MyParser parser) : DdlParser(parser)
{
    protected override ObjectName? ParseOptionalTriggerOnTableBeforeTime(ParserState state)
    {
        return null;
    }

    protected override bool ParseTriggerBodyStartsWithAs(ParserState state)
    {
        // In MySQL, the trigger body does not start with AS.
        return false;
    }

    protected override SqlValueList<TriggerEvent> ParseTriggerEvents(ParserState state)
    {
        // MySQL requires exactly one trigger event, whereas T-SQL allows multiple.
        return [ParseTriggerEvent(state)];
    }
}
