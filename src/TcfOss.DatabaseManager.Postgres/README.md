# TcfOss.DatabaseManager.Postgres

Initial PostgreSQL parsing support for DatabaseManager, targeting PostgreSQL 18
built-in general-purpose scalar types and built-in range/multirange types.

`PgParser` extends the Core parser and installs `PgDataTypeParser`. `PgLexer`
uses standard-conforming string quote escaping. PostgreSQL-specific type AST
records live in this provider's `BuiltIn` namespace as `PgDataType` variants.
`PgIntervalField` uses the project's generated string-enum representation.

Supported type syntax includes integer and floating-point aliases, serial
declarations, signed numeric scales, optional character and bit-string lengths,
time-zone clauses, interval field ranges, JSON, XML, UUID, network addresses,
geometric types, text-search types, snapshots, and log sequence numbers.
Type aliases are written using canonical spellings. Serial declarations remain
distinct from integers; their implied sequences are not constructed yet.

Type modifiers use Core's bounds-aware parsing helpers. PostgreSQL string and
bit lengths must be positive; numeric scales may be negative. Invalid modifier
values produce sourced `ParseException.ValueOutOfRange` diagnostics rather than
syntax expectation errors. Zero-length strings remain supported by Core and the
MySQL/MariaDB parser, but are rejected by PostgreSQL and SQL Server.

This is not yet a complete PostgreSQL grammar. Arrays, qualified/quoted type
references, user-defined types and domains, catalog/internal and routine
pseudo-types, PostgreSQL-specific casts (`::`), dollar-quoted strings, and routine
bodies are not implemented. Existing Core statement parsing is reused without
claiming full PostgreSQL syntax validation. Definition construction, database
communication, hosting/CLI registration, migration generation, and integration
tests are deferred.

Run the unit tests with:

```sh
dotnet test --project test/TcfOss.DatabaseManager.Postgres.Tests/TcfOss.DatabaseManager.Postgres.Tests.csproj
```