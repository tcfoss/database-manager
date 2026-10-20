namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public sealed record SchemaTableData(
    IReadOnlyDictionary<string, TableDto> TableInfoByName,
    IReadOnlyDictionary<string, IReadOnlyList<ColumnDto>> ColumnRowsByTable,
    IReadOnlyDictionary<string, IReadOnlyList<TableCheckDto>> CheckRowsByTable)
{
    public IReadOnlyList<ColumnDto> GetColumnRows(string tableName)
    {
        return ColumnRowsByTable[tableName];
    }

    public IReadOnlyList<TableCheckDto> GetCheckRows(string tableName)
    {
        return CheckRowsByTable.TryGetValue(tableName, out IReadOnlyList<TableCheckDto>? rows)
            ? rows
            : [];
    }
}
