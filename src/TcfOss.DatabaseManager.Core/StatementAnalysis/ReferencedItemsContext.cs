namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public enum ReferencedItemsContext
{
    SelectValueList,
    AlterTableTarget,
    DropObjectStatement,
    SelectItem,
    UpdateClause,
    UpdateColumn,
    UpdateValue,
    InsertTarget,
    InsertColumnList,
    InsertValueList,
    FromClause,
    WhereClause,
    JoinCondition,
    GroupByClause,
    OrderByClause,
    HavingClause,
    TruncateTarget,
    CreateTriggerTable,
    CreateIndexTable
}
