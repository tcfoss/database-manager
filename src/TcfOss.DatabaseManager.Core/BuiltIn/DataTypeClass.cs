namespace TcfOss.DatabaseManager.Core.BuiltIn;

#pragma warning disable CA1720 // Identifier contains type name

public enum DataTypeClass
{
    Integral,
    ExactDecimal,
    ApproximateDecimal,
    String,
    Binary,
    DateTime,
    Date,
    Time,
    Year,
    Boolean,
    Set,
    Enum,
    Geometry,
    Guid,
    Interval,
    Json,
    Xml,
    Network,
    TextSearch,
    Range,
    Multirange,
    System,
    Custom,
    Array,
}
