using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgTriggerConstraintOptions(ObjectIdentifier? FromTable, bool Deferrable, bool InitiallyDeferred);
