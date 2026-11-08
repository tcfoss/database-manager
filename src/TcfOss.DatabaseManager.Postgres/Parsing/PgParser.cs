using TcfOss.DatabaseManager.Core.BuiltIn;
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
        PrecedenceManager = new PrecedenceManager(usePostgresOperatorPrecedence: true);
        ComponentParser = new PgComponentParser(this);
        DataTypeParser = new PgDataTypeParser(this);
        ExpressionParser = new ExpressionParser(this);
        SelectParser = new SelectParser(this);
        DmlParser = new DmlParser(this);
        DdlParser = new PgDdlParser(this);
        TableParser = new PgTableParser(this);
        ControlFlowParser = new ControlFlowParser(this);
    }

    protected override CreateObjectOptions ParseCreateObjectOptions(ParserState state)
    {
        CreateOrLabel? createOrLabel = ComponentParser.ParseCreateOrLabel(state);
        bool temporary = state.ParseKeywordsAny(Keyword.TEMPORARY, Keyword.TEMP) != Keyword.undefined;
        return new CreateObjectOptions
        {
            CreateOrLabel = createOrLabel,
            Temporary = temporary,
            AllSupportedTypes = ["TABLE", "TRIGGER", "CONSTRAINT TRIGGER", "VIEW", "FUNCTION", "PROCEDURE", "INDEX", "TYPE", "DOMAIN"]
        };
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
            Keyword.INDEX or Keyword.UNIQUE or Keyword.FULLTEXT or Keyword.SPATIAL or Keyword.CLUSTERED or Keyword.NONCLUSTERED => DdlParser.ParseCreateIndex(state, options.CreateOrLabel),
            _ => throw state.ExpectedException(options.AllSupportedTypes),
        };
    }
}
