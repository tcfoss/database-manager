namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct SchemaConstraintDto()
{
    public required string TableName { get; init; }
    public required string ConstraintName { get; init; }
    public required string ConstraintType { get; init; }
}
