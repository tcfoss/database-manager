namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct TableCheckDto()
{
    public required string TableName { get; init; }
    public required string ConstraintName { get; init; }
    public required string CheckClause { get; init; }
    public string Level { get; init; } = "TABLE";
}
