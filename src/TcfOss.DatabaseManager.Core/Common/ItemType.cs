namespace TcfOss.DatabaseManager.Core.Common;

public enum ItemType
{
    Unknown,
    Table,
    TableColumn,
    View,
    ViewColumn,
    DerivedTable, // e.g. CTEs, subqueries
    DerivedColumn, // e.g. columns from CTEs, subqueries
    Function,
    Procedure,
    Trigger,
    Variable,
    Event,
    BuiltInFunction
}
