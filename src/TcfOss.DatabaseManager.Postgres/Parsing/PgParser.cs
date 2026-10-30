using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgParser : Parser
{
    public PgParser()
    {
        ComponentParser = new PgComponentParser(this);
        DataTypeParser = new PgDataTypeParser(this);
        ExpressionParser = new ExpressionParser(this);
        SelectParser = new SelectParser(this);
        DmlParser = new DmlParser(this);
        DdlParser = new DdlParser(this);
        TableParser = new TableParser(this);
        ControlFlowParser = new ControlFlowParser(this);
    }
}
