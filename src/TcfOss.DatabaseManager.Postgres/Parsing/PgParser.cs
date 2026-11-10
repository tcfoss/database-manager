using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgParser : Parser
{
    public override bool SupportsMySqlIntervalExpressionSyntax => false;

    public PgParser()
    {
        PrecedenceManager = new PgPrecedenceManager();
        ComponentParser = new PgComponentParser(this);
        DataTypeParser = new PgDataTypeParser(this);
        ExpressionParser = new PgExpressionParser(this);
        SelectParser = new SelectParser(this);
        DmlParser = new PgDmlParser(this);
        DdlParser = new PgDdlParser(this);
        TableParser = new PgTableParser(this);
        ControlFlowParser = new ControlFlowParser(this);
    }

    protected override Statement ParseNormalStatement(ParserState state, Token token)
    {
        if (state.Context.HasFlag(ParseContext.NarrowSqlOnly) && state.StatementLabel != null)
        {
            throw ParserState.ExpectedException("SQL statement", token);
        }
        return base.ParseNormalStatement(state, token);
    }

    protected override Statement ParseKeywordStatement(ParserState state, Word first)
    {
        if (!state.Context.HasFlag(ParseContext.NarrowSqlOnly))
        {
            return base.ParseKeywordStatement(state, first);
        }
        return first.Keyword switch
        {
            Keyword.DECLARE when state.PeekNth(2) is not Word { Keyword: Keyword.CURSOR } =>
                throw ParserState.ExpectedException("SQL statement", first),
            Keyword.LEAVE or Keyword.ITERATE or Keyword.OPEN or Keyword.FETCH or Keyword.USE =>
                throw ParserState.ExpectedException("SQL statement", first),
            _ => base.ParseKeywordStatement(state, first)
        };
    }

    protected override Statement ParseBegin(ParserState state)
    {
        if (state.Context.HasFlag(ParseContext.NarrowSqlOnly) && !state.PeekKeywordsAll(Keyword.BEGIN, Keyword.ATOMIC))
        {
            throw state.ExpectedException("BEGIN ATOMIC");
        }
        return base.ParseBegin(state);
    }

    protected override CreateObjectOptions ParseCreateObjectOptions(ParserState state)
    {
        CreateOrLabel? createOrLabel = ComponentParser.ParseCreateOrLabel(state);
        bool temporary = state.ParseKeywordsAny(Keyword.TEMPORARY, Keyword.TEMP) != Keyword.undefined;
        return new CreateObjectOptions
        {
            CreateOrLabel = createOrLabel,
            Temporary = temporary,
            AllSupportedTypes = ["TABLE", "TRIGGER", "CONSTRAINT TRIGGER", "VIEW", "MATERIALIZED VIEW", "FUNCTION", "PROCEDURE", "INDEX", "TYPE", "DOMAIN"]
        };
    }

    protected override Call ParseCall(ParserState state)
    {
        Token start = state.Peek();
        Call call = base.ParseCall(state);
        var invocation = (FunctionCall)call.Expression;
        var arguments = (FunctionArguments.List)invocation.Arguments;
        if (arguments.DuplicateTreatment != null || arguments.Clauses is { Count: > 0 })
        {
            throw ParserState.ExpectedCategoryException("procedure arguments", start);
        }

        bool namedSeen = false;
        if (arguments.Arguments != null)
        {
            for (int index = 0; index < arguments.Arguments.Count; index++)
            {
                FunctionArgument argument = arguments.Arguments[index];
                if (argument is FunctionArgument.Variadic variadic)
                {
                    if (index != arguments.Arguments.Count - 1 || variadic.Argument is FunctionArgument.Variadic)
                    {
                        throw ParserState.ExpectedCategoryException("final variadic procedure argument", start);
                    }
                    argument = variadic.Argument;
                }
                switch (argument)
                {
                    case FunctionArgument.Named { Argument: FunctionArgumentExpression.Wildcard }:
                    case FunctionArgument.Unnamed { Argument: FunctionArgumentExpression.Wildcard }:
                    case FunctionArgument.Unnamed when namedSeen:
                        throw ParserState.ExpectedCategoryException("procedure arguments", start);
                    case FunctionArgument.Named:
                        namedSeen = true;
                        break;
                }
            }
        }
        return call;
    }

    protected override void GuardCreateOptions(ParserState state, Word objectTypeToken, CreateObjectOptions options)
    {
        Keyword kw = objectTypeToken.Keyword;
        if (options.CreateOrLabel != null && kw is not (Keyword.VIEW or Keyword.FUNCTION or Keyword.PROCEDURE or Keyword.TRIGGER))
        {
            throw state.ExpectedException("VIEW", "FUNCTION", "PROCEDURE", "TRIGGER");
        }
        if (options.Temporary && kw is not (Keyword.TABLE or Keyword.VIEW))
        {
            throw state.ExpectedException("TABLE", "VIEW");
        }
    }

    protected override Statement ParseCreate(ParserState state)
    {
        state.ExpectKeyword(Keyword.CREATE);

        CreateObjectOptions options = ParseCreateObjectOptions(state);
        if (state.Peek() is not Word objectTypeToken)
        {
            throw state.ExpectedException(options.AllSupportedTypes);
        }
        GuardCreateOptions(state, objectTypeToken, options);

        return objectTypeToken.Keyword switch
        {
            Keyword.DOMAIN => DdlParser.ParseCreateDomain(state),
            Keyword.TYPE => DdlParser.ParseCreateType(state),
            Keyword.TABLE => TableParser.ParseCreate(state, options.CreateOrLabel == CreateOrLabel.OrReplace, options.Temporary),
            Keyword.TRIGGER or Keyword.CONSTRAINT => DdlParser.ParseCreateTrigger(state, options.CreateOrLabel),
            Keyword.FUNCTION => DdlParser.ParseCreateFunction(state, false, options.CreateOrLabel, null),
            Keyword.PROCEDURE or Keyword.PROC => DdlParser.ParseCreateProcedure(state, options.CreateOrLabel, null),
            Keyword.VIEW => DdlParser.ParseCreateView(state, options.CreateOrLabel, null, null, null),
            Keyword.MATERIALIZED => DdlParser.ParseCreateMaterializedView(state),
            Keyword.INDEX or Keyword.UNIQUE or Keyword.FULLTEXT or Keyword.SPATIAL or Keyword.CLUSTERED or Keyword.NONCLUSTERED => DdlParser.ParseCreateIndex(state, options.CreateOrLabel),
            _ => throw state.ExpectedException(options.AllSupportedTypes),
        };
    }
}
