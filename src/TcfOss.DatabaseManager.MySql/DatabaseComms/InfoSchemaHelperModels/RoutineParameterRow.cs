namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;

public sealed class RoutineParameterRow
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required int OrdinalPosition { get; init; }
    public required RoutineParameterDto Value { get; init; }
}
