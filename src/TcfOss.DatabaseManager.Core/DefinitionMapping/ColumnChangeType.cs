namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

[Flags]
public enum ColumnChangeType
{
    None = 0b0,
    DataType = 0b0001,
    Nullability = 0b0010,
    SetDefault = 0b0100,
    DropDefault = 0b1000,
    CheckConstraint = 0b0001_0000,
    AddAutoIncrement = 0b0010_0000,
    DropAutoIncrement = 0b0100_0000,
    Comment = 0b1000_0000,
    DropGeneration = 0b0001_0000_0000,
    GenerationExpression = 0b0010_0000_0000,
    Order = 0b0100_0000_0000,
};
