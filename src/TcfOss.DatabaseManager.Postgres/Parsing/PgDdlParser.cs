using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgDdlParser(Parser parser) : DdlParser(parser)
{
    protected override bool UseSqlRoutineTail(ParserState state) => true;

    protected override RoutineParamsSupportStyle GetRoutineParamsSupportStyle(bool isFunction)
    {
        return isFunction ? RoutineParamsSupportStyle.PostgreSqlFunction : RoutineParamsSupportStyle.PostgreSqlProcedure;
    }
}
