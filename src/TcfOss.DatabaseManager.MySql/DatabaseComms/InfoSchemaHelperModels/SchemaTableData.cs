using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public sealed record SchemaTableData(
    IReadOnlyDictionary<string, TableDto> TableInfoByName,
    IReadOnlyDictionary<string, IReadOnlyList<ColumnDto>> ColumnRowsByTable,
    IReadOnlyDictionary<string, IReadOnlyList<TableCheckDto>> CheckRowsByTable,
    IReadOnlyDictionary<string, TableIndexMetadata> IndexMetadataByTable,
    IReadOnlyDictionary<string, IReadOnlyList<ForeignKeyColumnDto>> ForeignKeyRowsByTable)
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

    public TableIndexMetadata GetTableIndexMetadata(string tableName)
    {
        return IndexMetadataByTable.TryGetValue(tableName, out TableIndexMetadata? metadata)
            ? metadata
            : new TableIndexMetadata(null, [], []);
    }

    public DatabaseComponentDict<MyForeignKey> GetForeignKeys(string tableName, SchemaIdentifier schemaId, QuoteStyle quoteStyle, NameHandling nameHandling)
    {
        var foreignKeys = new DatabaseComponentDict<MyForeignKey>();

        if (!ForeignKeyRowsByTable.TryGetValue(tableName, out IReadOnlyList<ForeignKeyColumnDto>? rows))
        {
            return foreignKeys;
        }

        foreach (IGrouping<string, ForeignKeyColumnDto> group in rows.GroupBy(r => r.ConstraintName))
        {
            List<ForeignKeyColumnDto> colinfo = [.. group];
            ForeignKeyColumnDto first = colinfo.First();

            var id = new Identifier(first.ConstraintName, quoteStyle);
            ReferentialAction updateAction = ReferentialAction.Parse(first.UpdateRule);
            ReferentialAction deleteAction = ReferentialAction.Parse(first.DeleteRule);

            var localCols = new SqlValueList<Identifier>(colinfo.Select(c => new Identifier(c.ColumnName, quoteStyle)));
            var refTable = ObjectIdentifier.FromStrings([first.ReferencedTableSchema!, first.ReferencedTableName!], schemaId, quoteStyle);
            var refCols = new SqlValueList<Identifier>(colinfo.Select(c => new Identifier(c.ReferencedColumnName!, quoteStyle)));

            foreignKeys.Add(Handle.Create(id, nameHandling), new MyForeignKey(localCols, refTable, refCols, id)
            {
                OnUpdate = updateAction,
                OnDelete = deleteAction
            });
        }

        return foreignKeys;
    }
}
