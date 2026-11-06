using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgTriggerConstraint(ObjectName? FromTable, bool? Deferrable, bool? InitiallyDeferred);
