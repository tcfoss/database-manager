namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;

public sealed class RoutineRow
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required RoutineDto Value { get; init; }
}
