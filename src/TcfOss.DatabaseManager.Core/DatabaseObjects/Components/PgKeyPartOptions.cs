using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public record PgKeyPartOptions(
    ObjectName? OperatorClass,
    SqlValueList<ObjectNameKvp>? OperatorClassParameters,
    NullsOrder? NullsOrder);
