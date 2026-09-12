namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct RoutineParameterDto()
{
    public required string ParameterName { get; init; }
    public required string DataType { get; init; }
    public string? CharacterSet { get; init; }
    public string? Collation { get; init; }
    public string? ParameterMode { get; init; }
}
