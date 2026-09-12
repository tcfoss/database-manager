namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct TriggerDto()
{
    public required string EventObjectTable { get; init; }
    public required string TriggerName { get; init; }
    public required string EventManipulation { get; init; }
    public required string ActionTiming { get; init; }
    public required string ActionStatement { get; init; }
    public required long ActionOrder { get; init; }
    public required string Definer { get; init; }
}
