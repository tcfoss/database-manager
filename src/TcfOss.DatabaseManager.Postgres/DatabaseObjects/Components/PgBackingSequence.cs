using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgBackingSequence(ObjectIdentifier? Name = null, PgIdentityOptions? Options = null);
