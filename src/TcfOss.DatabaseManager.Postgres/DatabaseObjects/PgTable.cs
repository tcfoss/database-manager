using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public record PgTable(ObjectIdentifier Name, SqlValueList<PgColumn> Columns) : IDatabaseObject<CreateTable>
{
    public ObjectType ObjectType => ObjectType.Table;

    public CreateTable ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        return new CreateTable(Name.ToObjectName(includeSchema ? 2 : 1),
            [.. Columns.Select(column => column.ToStatementColumn(manager))], []);
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return ToCreateStatement(true).GetReferencedItems(context);
    }

    public PseudoTable ToPseudoTable()
    {
        return new PseudoTable(Name.Name, Name, [.. Columns.Select(column => column.Name.Name)], PseudoTableType.Table);
    }
}
