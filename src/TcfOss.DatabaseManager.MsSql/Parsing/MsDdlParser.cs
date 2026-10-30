using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsDdlParser(MsParser parser) : DdlParser(parser)
{
    private readonly MsParser _p = parser;

    /// <summary>
    /// Parses a T-SQL routine parameter: <c>@name TYPE [= default] [OUT|OUTPUT]</c>.
    /// </summary>
    protected override StatementRoutineParameter ParseRoutineParameter(ParserState state)
    {
        Identifier name = _p.ComponentParser.ParseIdentifier(state);
        if (name.Sigil != SigilKind.Variable)
        {
            throw state.ExpectedException("parameter name (@name)");
        }
        DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReference(state);

        Value? defaultValue = null;
        if (state.ConsumeTokenIs<Equal>())
        {
            defaultValue = ValueParser.ParseValue(state);
        }

        OutputKeyword? output = null;
        if (state.ParseKeyword(Keyword.OUTPUT))
        {
            output = OutputKeyword.Output;
        }
        else if (state.ParseKeyword(Keyword.OUT))
        {
            output = OutputKeyword.Out;
        }

        return new StatementRoutineParameter.Undirected(name, dataType)
        {
            DefaultValue = defaultValue,
            Output = output,
        };
    }

    /// <summary>
    /// T-SQL allows the parameter list to omit parentheses entirely (or be
    /// empty). The bare form is detected by an <c>@</c>-prefixed identifier
    /// in the lookahead position.
    /// </summary>
    protected override SqlValueList<StatementRoutineParameter> ParseRoutineParameterList(ParserState state)
    {
        if (state.PeekIs<ParenOpen>())
        {
            return state.ParseParenthesizedCommaSeparated(ParseRoutineParameter, allowEmpty: true);
        }
        if (state.Peek() is Word { Sigil: SigilKind.Variable })
        {
            return state.ParseCommaSeparated(ParseRoutineParameter);
        }
        return [];
    }

    /// <summary>
    /// Narrows <see cref="DdlParser.ParseTriggerTime"/> to reject <c>BEFORE</c>,
    /// which is not supported in T-SQL.
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

    protected override bool ParseTriggerBodyStartsWithAs(ParserState state)
    {
        // In T-SQL, <c>AS</c> must precede the trigger body.
        state.ExpectKeyword(Keyword.AS);
        return true;
    }

}
