using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

/// <summary>
/// MySQL-style routine argument direction
/// </summary>
[StringEnum("IN")]
[StringEnum("OUT")]
[StringEnum("INOUT", "InOut", "IN OUT")]
public sealed partial class RoutineParameterDirection;
