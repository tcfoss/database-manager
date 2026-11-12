using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgIndexKeyOptions(ObjectName? OperatorClass,
    SqlValueList<PgStorageParameter>? OperatorClassParameters, NullsOrder? NullsOrder);
