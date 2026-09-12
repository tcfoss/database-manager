namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct TableDto()
{
    public required string TableName { get; init; }
    public required string Engine { get; init; }
    public required string CharacterSet { get; init; }
    public required string Collation { get; init; }
    public string? TableComment { get; init; }
}
