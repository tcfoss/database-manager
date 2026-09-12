using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

/// <summary>
/// T-SQL DDL parser. Most parsing is shared with Core <see cref="DdlParser"/>;
/// this subclass narrows behavior where T-SQL is stricter than the kitchen-sink
/// Core implementation: rejects MySQL-only modifiers (DEFINER, ALGORITHM, SQL
/// SECURITY, AGGREGATE), rejects <c>BEFORE</c> trigger time, and replaces the
/// MySQL-style routine parameter form with the T-SQL <c>@name TYPE [= default]
/// [OUT|OUTPUT]</c> form.
/// </summary>
public class MsDdlParser(MsParser parser) : DdlParser(parser)
{
    private readonly MsParser _p = parser;

    /// <summary>
    /// Parses a T-SQL routine parameter: <c>@name TYPE [= default] [OUT|OUTPUT]</c>.
    /// </summary>
    protected override RoutineParameter ParseRoutineParameter(ParserState state)
    {
        Identifier name = _p.ComponentParser.ParseIdentifier(state);
        if (name.Sigil != SigilKind.Variable)
        {
            throw state.ExpectedException("parameter name (@name)");
        }
        DataType dataType = _p.DataTypeParser.ParseDataType(state);

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

        return new RoutineParameter.Undirected(name, dataType)
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
    protected override SqlValueList<RoutineParameter> ParseRoutineParameterList(ParserState state)
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

    public override CreateProcedure ParseCreateProcedure(ParserState state, CreateOrLabel? createOrLabel = null, Definer? definer = null)
    {
        if (definer != null)
        {
            throw state.ExpectedException("PROCEDURE");
        }
        return base.ParseCreateProcedure(state, createOrLabel, definer);
    }

    public override CreateFunction ParseCreateFunction(ParserState state, bool aggregate = false, CreateOrLabel? createOrLabel = null, Definer? definer = null)
    {
        if (aggregate || definer != null)
        {
            throw state.ExpectedException("FUNCTION");
        }
        return base.ParseCreateFunction(state, aggregate, createOrLabel, definer);
    }

    public override CreateTrigger ParseCreateTrigger(ParserState state, CreateOrLabel? createOrLabel = null, Definer? definer = null)
    {
        if (definer != null)
        {
            throw state.ExpectedException("TRIGGER");
        }
        return base.ParseCreateTrigger(state, createOrLabel, definer);
    }

    public override CreateView ParseCreateView(ParserState state, CreateOrLabel? createOrLabel, Definer? definer, ViewAlgorithm? viewAlgorithm, SecurityContext? securityContext)
    {
        if (definer != null || viewAlgorithm != null || securityContext != null)
        {
            throw state.ExpectedException("VIEW");
        }
        return base.ParseCreateView(state, createOrLabel, definer, viewAlgorithm, securityContext);
    }
}
