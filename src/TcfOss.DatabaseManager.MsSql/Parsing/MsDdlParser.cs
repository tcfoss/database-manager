using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsDdlParser(MsParser parser) : DdlParser(parser)
{
    protected override bool SupportsIndexMethodAfterTableName => false;
    protected override bool SupportsPostgresIndexSyntax => false;

    protected override bool UseSqlRoutineTail(ParserState state) => false;

    protected override bool SupportsOmittedFunctionReturn => false;
    protected override bool SupportsPostgresTriggerSyntax => false;
    protected override bool SupportsDropRoutineSignatures => false;

    protected override ObjectName? ParseDropIndexOnObject(ParserState state)
    {
        state.ExpectKeyword(Keyword.ON, advance: false);
        return base.ParseDropIndexOnObject(state);
    }

    protected override bool SupportsDropObjectType(DroppableObject objectType)
    {
        return objectType != DroppableObject.MaterializedView && objectType != DroppableObject.Domain
            && objectType != DroppableObject.Routine;
    }

    protected override ObjectName? ParseDropTriggerOnObject(ParserState state)
    {
        return null;
    }

    protected override bool SupportsDropObjectList(DroppableObject objectType)
    {
        return objectType == DroppableObject.Table || objectType == DroppableObject.View || objectType == DroppableObject.Sequence;
    }

    protected override bool ParseOptionalDropTemporary(ParserState state)
    {
        return false;
    }

    protected override PgDropBehavior? ParseOptionalDropBehavior(ParserState state, DroppableObject objectType)
    {
        return null;
    }

    protected override RoutineParamsSupportStyle GetRoutineParamsSupportStyle(bool isFunction)
    {
        return isFunction ? RoutineParamsSupportStyle.MsSqlFunction : RoutineParamsSupportStyle.MsSqlProcedure;
    }

    protected override StatementFunctionReturn ParseFunctionReturn(ParserState state)
    {
        return ParseScalarFunctionReturn(state);
    }

    /// <summary>
    /// T-SQL allows the parameter list to omit parentheses entirely (or be
    /// empty). The bare form is detected by an <c>@</c>-prefixed identifier
    /// in the lookahead position.
    /// </summary>
    protected override SqlValueList<StatementRoutineParameter> ParseRoutineParameterList(ParserState state, bool isFunction)
    {
        RoutineParamsSupportStyle style = GetRoutineParamsSupportStyle(isFunction);
        if (state.PeekIs<ParenOpen>())
        {
            return state.ParseParenthesizedCommaSeparated(parameterState =>
                ParseRoutineParameter(parameterState, style), allowEmpty: true);
        }
        if (state.Peek() is Word { Sigil: SigilKind.Variable })
        {
            return state.ParseCommaSeparated(parameterState =>
                ParseRoutineParameter(parameterState, style));
        }
        return [];
    }

    /// <summary>
    /// <code>
    /// <![CDATA[
    /// AFTER | INSTEAD OF | FOR
    /// ]]>
    /// </code>
    /// </summary>
    public override TriggerTime ParseTriggerTime(ParserState state)
    {
        if (state.PeekKeyword(Keyword.BEFORE))
        {
            throw state.ExpectedException("AFTER", "INSTEAD OF", "FOR");
        }
        return base.ParseTriggerTime(state);
    }

    protected override TriggerOrder? ParseOptionalTriggerOrder(ParserState state)
    {
        return null;
    }

    protected override TriggerEvent ParseTriggerEvent(ParserState state)
    {
        return ParseTriggerEventNarrow(state);
    }

    protected override List<TriggerEvent>? ParseAdditionalTriggerEventsViaOr(ParserState state)
    {
        return null;
    }

    protected override bool ParseTriggerBodyStartsWithAs(ParserState state)
    {
        // In T-SQL, <c>AS</c> must precede the trigger body.
        state.ExpectKeyword(Keyword.AS);
        return true;
    }

    protected override SqlValueList<PgStorageParameter>? ParseOptionalPgViewOptions(ParserState state)
    {
        return null;
    }
}
