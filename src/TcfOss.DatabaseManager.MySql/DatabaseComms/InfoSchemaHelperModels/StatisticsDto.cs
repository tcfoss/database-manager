namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct StatisticsDto()
{
    public required string TableName { get; init; }
    public required string IndexName { get; init; }
    public required string ColumnName { get; init; }
    public required string IndexType { get; init; }
    public long? SubPart { get; init; }
    public string? Collation { get; init; }
    public required string IndexComment { get; init; }
    public uint SeqInIndex { get; init; }
}
