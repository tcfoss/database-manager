namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct ColumnDto()
{
    public required string TableName { get; init; }
    public required string ColumnName { get; init; }
    public required string ColumnType { get; init; }
    public string? CharacterSetName { get; init; }
    public string? CollationName { get; init; }
    public required string IsNullable { get; init; }
    public string? ColumnDefault { get; init; }
    public string? GenerationExpression { get; init; }
    public string? Extra { get; init; }
    public required string ColumnComment { get; init; }
    public string? CheckClause { get; init; }
}
