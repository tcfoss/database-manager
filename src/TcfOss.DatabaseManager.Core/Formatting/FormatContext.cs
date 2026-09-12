namespace TcfOss.DatabaseManager.Core.Formatting;

public enum FormatContext
{
    SelectStatement,
    SelectItem,
    FromClause,
    TableRelation,
    Join,
    JoinCondition,
    WhereClause,
    OrderByClause,
    GroupByClause,
    HavingClause,

    UpdateStatement,
    UpdateSetBlock,

    InsertStatement,
    InsertColumns,
    InsertValues,

    DeleteStatement,

    InsertOnDuplicateUpdate,

    AssignmentTarget,
    AssignmentValue,
}
