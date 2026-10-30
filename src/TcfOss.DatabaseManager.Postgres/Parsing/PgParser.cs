using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgParser : Parser
{
    public PgParser()
    {
        DataTypeParser = new PgDataTypeParser();
    }
}