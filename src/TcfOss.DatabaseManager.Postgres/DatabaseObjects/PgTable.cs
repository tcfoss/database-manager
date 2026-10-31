using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public record PgTable(ObjectIdentifier Name, SqlValueList<PgColumn> Columns) : IHaveObjectIdentifierName
{
    public PseudoTable ToPseudoTable()
    {
        return new PseudoTable(Name.Name, Name, [.. Columns.Select(column => column.Name.Name)], PseudoTableType.Table);
    }
}
