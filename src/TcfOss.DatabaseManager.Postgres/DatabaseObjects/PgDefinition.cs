using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;
using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public class PgDefinition
{
    public DatabaseObjectDict<PgTable> Tables { get; init; } = [];
    public DatabaseObjectDict<PgCustomType> CustomTypes { get; init; } = [];
    public DatabaseObjectDict<PgStoredFunction> Functions { get; init; } = [];
    public DatabaseObjectDict<PgStoredProcedure> Procedures { get; init; } = [];
    public DatabaseObjectDict<DatabaseComponentDict<PgTrigger>> Triggers { get; init; } = [];
    public ValueDict<PgCommentTarget, string> Comments { get; init; } = [];
}
