using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MsSql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MsSql.DatabaseObjects;

public record MsTable(ObjectIdentifier Name, SqlValueList<MsColumn> Columns)
    : IDatabaseObject<CreateTable>
{
    public ObjectType ObjectType => ObjectType.Table;

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context) => throw new NotImplementedException();
    public CreateTable ToCreateStatement(bool includeSchema, DifferFormatManager manager)
    {
        throw new NotImplementedException();
    }
}