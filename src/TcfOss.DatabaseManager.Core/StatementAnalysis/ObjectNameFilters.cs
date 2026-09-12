namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

[Flags]
public enum ObjectNameFilters
{
    None = 0,
    Unknown = 1 << 0,
    Table = 1 << 1,
    TableColumn = 1 << 2,
    View = 1 << 3,
    ViewColumn = 1 << 4,
    DerivedTable = 1 << 5,
    DerivedColumn = 1 << 6,
    Function = 1 << 7,
    Procedure = 1 << 8,
    Variable = 1 << 9,
    Event = 1 << 10,
    BuiltInFunction = 1 << 11,

    // Combination flags
    AnyObject = Table | View | Function | Procedure | Event,
    AnyColumn = TableColumn | ViewColumn | DerivedColumn,
    Derived = DerivedTable | DerivedColumn,
    All = Unknown | Table | TableColumn | View | ViewColumn | DerivedTable | DerivedColumn | Function | Procedure | Variable | Event | BuiltInFunction,
}
