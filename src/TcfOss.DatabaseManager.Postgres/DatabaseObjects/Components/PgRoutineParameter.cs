using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgRoutineParameter(Identifier Name, DataType DataType)
{
    public RoutineParameterDirection? Direction { get; init; }
    public Value? DefaultValue { get; init; }
    public OutputKeyword? Output { get; init; }
}
