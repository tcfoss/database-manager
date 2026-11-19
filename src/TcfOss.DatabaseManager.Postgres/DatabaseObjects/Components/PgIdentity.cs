namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgIdentity(bool Always, PgBackingSequence BackingSequence);
