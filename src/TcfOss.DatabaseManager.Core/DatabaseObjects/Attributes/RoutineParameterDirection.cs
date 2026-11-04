using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

/// <summary>
/// MySQL-style routine argument direction
/// SQL routine argument mode
/// </summary>
[StringEnum("IN")]
[StringEnum("OUT")]
[StringEnum("INOUT", "InOut", "IN OUT")]
[StringEnum("VARIADIC")]
public sealed partial class RoutineParameterDirection;
