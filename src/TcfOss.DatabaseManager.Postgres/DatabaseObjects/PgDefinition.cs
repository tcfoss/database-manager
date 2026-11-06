using TcfOss.DatabaseManager.Core;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public class PgDefinition
{
    public DatabaseObjectDict<PgTable> Tables { get; init; } = [];
    public DatabaseObjectDict<PgCustomType> CustomTypes { get; init; } = [];
    public DatabaseObjectDict<PgStoredFunction> Functions { get; init; } = [];
    public DatabaseObjectDict<PgStoredProcedure> Procedures { get; init; } = [];
    public SqlValueList<PgTrigger> Triggers { get; init; } = [];
}
