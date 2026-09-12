namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public sealed record SchemaIndexData(IReadOnlyDictionary<string, TableIndexMetadata> TableIndexMetadataByName)
{
    public TableIndexMetadata GetTableIndexMetadata(string tableName)
    {
        return TableIndexMetadataByName.TryGetValue(tableName, out TableIndexMetadata? metadata)
            ? metadata
            : new TableIndexMetadata(null, [], []);
    }
}
