namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct RoutineDto()
{
    public string? DataType { get; init; }
    public required string Definer { get; init; }
    public required string SecurityType { get; init; }
    public required string SqlDataAccess { get; init; }
    public string? RoutineComment { get; init; }
    public required string IsDeterministic { get; init; }
    public string? RoutineDefinition { get; init; }
}
