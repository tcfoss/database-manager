namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

[Flags]
public enum ColumnChangeType
{
    None = 0,
    DataType = 1 << 0,
    SetNull = 1 << 1,
    SetNotNull = 1 << 2,
    SetDefault = 1 << 3,
    DropDefault = 1 << 4,
    CheckConstraint = 1 << 5,
    AddAutoIncrement = 1 << 6,
    DropAutoIncrement = 1 << 7,
    Comment = 1 << 8,
    DropGeneration = 1 << 9,
    GenerationExpression = 1 << 10,
    Order = 1 << 11,
    OnUpdate = 1 << 12,
};
