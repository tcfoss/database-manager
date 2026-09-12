namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct ForeignKeyColumnDto()
{
    public required string TableName { get; init; }
    public required string ConstraintName { get; init; }
    public required string UpdateRule { get; init; }
    public required string DeleteRule { get; init; }
    public required string ColumnName { get; init; }
    public string? ReferencedTableSchema { get; init; }
    public string? ReferencedTableName { get; init; }
    public string? ReferencedColumnName { get; init; }
}
