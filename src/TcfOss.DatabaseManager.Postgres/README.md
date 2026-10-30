# TcfOss.DatabaseManager.Postgres

Initial PostgreSQL parsing support for DatabaseManager, targeting PostgreSQL 18
built-in general-purpose scalar types, built-in range/multirange types, and
enumerated/composite type declarations.

`PgParser` extends the Core parser and installs `PgDataTypeParser`. `PgLexer`
uses standard-conforming string quote escaping. PostgreSQL-specific type AST
records live in this provider's `BuiltIn` namespace as `PgDataType` variants.
`PgIntervalField` uses the project's generated string-enum representation.

`PgComponentParser` supplies identifier validation to Core's shared object-name
parser. It accepts unquoted and double-quoted identifiers while rejecting
single-quoted strings, bracket/backtick quoting, variable sigils, empty names,
and invalid unquoted name starts. The labelled `ParseObjectName` overload applies
the requested diagnostic label to every identifier in a qualified name.

Supported type syntax includes integer and floating-point aliases, serial
declarations, signed numeric scales, optional character and bit-string lengths,
time-zone clauses, interval field ranges, JSON, XML, UUID, network addresses,
geometric types, text-search types, snapshots, and log sequence numbers.
Type aliases are written using canonical spellings. Serial declarations remain
distinct from integers; their implied sequences are not constructed yet.

`CREATE TYPE ... AS ENUM (...)` and `CREATE TYPE ... AS (...)` are parsed by
Core's `DdlParser`, producing `CreateType.Enumerated` and `CreateType.Composite`
statements. Plain Core `Parser`/`GenericLexer` callers can obtain these ASTs
without the PostgreSQL provider. Composite fields retain parsed type references
and optional collations; enum labels retain their order. Empty definitions are
supported. Type existence and dependency resolution remain deferred.

`Parser.ParseCreate` is protected and virtual. Provider overrides narrow its
dispatch while reusing the shared modifier prefix and Core parsing methods.
MySQL/MariaDB and SQL Server reject PostgreSQL type/domain declarations;
PostgreSQL and SQL Server omit the MySQL event branch. `CREATE DOMAIN` grammar
is not implemented yet.

Parsed columns, casts, routine parameters, and function return declarations use
Core's `DataTypeReference`: `BuiltIn` wraps a concrete `DataType`, while `Named`
retains an unresolved `ObjectName`. `ParseDataTypeReference` accepts unqualified,
qualified, and double-quoted names, preserving spelling, quoting, and source
locations. `ParseDataType` remains the built-in-only entry point. Invalid
built-in syntax is not retried as a named reference.

Each `DataTypeParser` receives its owning `Parser` and uses that parser's
`ComponentParser` for named references. Core provides this permissive reference
parsing directly; PostgreSQL inherits it with PostgreSQL identifier validation.
MySQL/MariaDB and SQL Server override reference parsing to return built-in
references only. MySQL retains its specialized conversion-type grammar.

Create statements carry `StatementRoutineParameter` values containing parsed
type references. Definition-level `RoutineParameter` values retain concrete
`DataType` values instead. Construction converts statement parameters after
resolving their types; `ToStatementParameter()` converts definition parameters
back for SQL generation while preserving direction, defaults, and output markers.

References are not looked up, case-folded, or verified to exist during parsing.
Resolution and conversion to normalized identities belong to future definition
construction. Named references also work in signatures accepted by the existing
Core routine grammar; full PostgreSQL routine syntax is still deferred.

Type modifiers use Core's bounds-aware parsing helpers. PostgreSQL string and
bit lengths must be positive; numeric scales may be negative. Invalid modifier
values produce sourced `ParseException.ValueOutOfRange` diagnostics rather than
syntax expectation errors. Zero-length strings remain supported by Core and the
MySQL/MariaDB parser, but are rejected by PostgreSQL and SQL Server.

This is not yet a complete PostgreSQL grammar. Arrays, custom type modifiers,
domain declarations, range/base/shell type declarations, catalog/internal and routine
pseudo-types, PostgreSQL-specific casts (`::`), dollar-quoted strings, and routine
bodies are not implemented. Existing Core statement parsing is reused without
claiming full PostgreSQL syntax validation. Definition construction, database
communication, hosting/CLI registration, migration generation, and integration
tests are deferred.

Run the unit tests with:

```sh
dotnet test --project test/TcfOss.DatabaseManager.Postgres.Tests/TcfOss.DatabaseManager.Postgres.Tests.csproj
```