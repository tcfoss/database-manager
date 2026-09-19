namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

[Flags]
public enum ColumnChangeType
{
    None = 0b0,
    DataType = 0b0001,
    SetNull = 0b0010,
    SetNotNull = 0b0100,
    SetDefault = 0b1000,
    DropDefault = 0b0001_0000,
    CheckConstraint = 0b0010_0000,
    AddAutoIncrement = 0b0100_0000,
    DropAutoIncrement = 0b1000_0000,
    Comment = 0b0001_0000_0000,
    DropGeneration = 0b0010_0000_0000,
    GenerationExpression = 0b0100_0000_0000,
    Order = 0b1000_0000_0000,
};
