# TcfOss.DatabaseManager.Postgres

Initial PostgreSQL parsing and declaration-collection support for DatabaseManager, targeting PostgreSQL 18
built-in general-purpose scalar types, built-in range/multirange types,
enumerated/composite type declarations, and domain declarations.

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
supported. Parsing does not check type existence; definition construction resolves
the references after all declarations have been collected.
Enum labels and decoded composite attribute names are checked for ordinal,
case-sensitive uniqueness during parsing. Definition construction retains these
guards for manually assembled statements and applies configured name handling
to detect additional composite-name collisions.

`Parser.ParseCreate` is protected and virtual. Provider overrides narrow its
dispatch while reusing the shared modifier prefix and Core parsing methods.
MySQL/MariaDB and SQL Server reject PostgreSQL type/domain declarations;
PostgreSQL and SQL Server omit the MySQL event branch.

`CREATE DOMAIN name [AS] data_type` is also parsed by Core's `DdlParser`,
producing a `CreateDomain` statement. It supports optional collation, a default
expression, and ordered named or unnamed `NULL`, `NOT NULL`, and `CHECK`
constraints. Optional `AS` and constraint names are preserved. PostgreSQL uses
its own type and identifier parsers for the base type, collation, and names.
Unquoted `VALUE` within a domain check becomes a distinct `DomainValue`
expression with no column dependency; quoted identifiers and expressions
outside domain checks retain their normal interpretation. Domain checks and
defaults are parsed, not evaluated. Definition construction resolves domain base
types and validates constraint names and nullability conflicts.
Parsing also rejects redundant `NOT NULL` and conflicting `NULL`/`NOT NULL`
clauses, pointing to the offending clause; repeated `NULL` remains allowed.
Builder validation remains in place for manually constructed statements.

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
Resolution and conversion to normalized identities happen during definition
construction. Named references also work in signatures accepted by the existing
Core routine grammar; full PostgreSQL routine syntax is still deferred.

Type modifiers use Core's bounds-aware parsing helpers. PostgreSQL string and
bit lengths must be positive; numeric scales may be negative. Invalid modifier
values produce sourced `ParseException.ValueOutOfRange` diagnostics rather than
syntax expectation errors. Zero-length strings remain supported by Core and the
MySQL/MariaDB parser, but are rejected by PostgreSQL and SQL Server.

This is not yet a complete PostgreSQL grammar. Arrays, custom type modifiers,
range/base/shell type declarations, catalog/internal and routine
pseudo-types, PostgreSQL-specific casts (`::`), dollar-quoted strings, and routine
bodies are not implemented. Existing Core statement parsing is reused without
claiming full PostgreSQL syntax validation. Database communication, hosting/CLI
registration, migration generation, and integration
tests are deferred.

## Definition Construction

`PgConfig` and `PgSchemaMapping` provide the minimal configuration contracts;
configuration loading, credentials, and provider registration are not wired up.
Callers currently supply `QuoteStyle.Ansi` and
`NameHandling.LowercaseUnlessQuoted` when creating a PostgreSQL configuration.

`PgDefinition` contains tables, custom types, functions, and procedures.
`PgTable` contains columns;
`PgCustomType` distinguishes enums, composites, and domains. Definition-level
columns, composite fields, and domain base types use Core's `DataType` directly.
Built-ins retain their ordinary datatype records; resolved custom types use
`PgDataType.Custom(ObjectIdentifier Name)` with `DataTypeClass.Custom`. The custom
datatype prints its schema-qualified identity using the identifier's configured
quote style, without expanding its declaration. Parsed `DataTypeReference`
remains separate and may contain unresolved `ObjectName` values.
Column defaults and nullability are retained, as are composite-field collations
and domain defaults, collations, and ordered named constraints. Domains retain
their own identities and are not flattened into their underlying types.
`PgColumn` uses shared `ColumnOption.Nullability` and `ColumnOption.ColumnDefault`
records, keeping constraint names with their options, distinguishing absent
nullability from explicit `NULL`, and preserving literal/expression defaults.
Default conversion is shared with MySQL through Core's protected
`TableBuilder.NormalizeDefault` helper. The base builder owns its injected
`INormalizeComponents` as a protected property and uses it directly for value
and constraint-name normalization. PostgreSQL's expression hooks currently
return their inputs unchanged; expression defaults use the same expression for
both SQL representation and normalized equality. Provider-specific expression
normalization rules remain deferred.
Routine signatures use Core's `RoutineParameter` directly. `PgStoredFunction`
and `PgStoredProcedure` derive from Core's abstract routine models, using resolved
`DataType` values for parameters and function return types. Core's raw-body text
contract is retained: raw text is copied into generated statement metadata and
excluded from routine equality and hashing.

`PgDefinitionBuilder.ProcessStatements` collects table, enum, composite, and
domain declarations, plus the currently supported Core routine forms, across
sources, retaining their original statements and
source references in private, strongly typed per-kind dictionaries. A private
`Declaration<TStatement>` groups type statements with their qualified names and
source references. The shared `GetAndValidateName` helper computes handles from
original statement names, independently of output quoting. A table and a custom type
cannot claim the same handle. `PgTableBuilder` retains typed column declarations,
including their parsed options and unresolved type references, and exposes a
column-only pseudo-table. Forward references are retained without lookup.

`ToDefinition()` builds a complete type-name registry before constructing any
resolved objects. `PgTypeResolutionManager` resolves named references using
`PgConfig.SearchPath` (default: `public`) in order. Qualified names bypass that
path. Missing path entries are skipped; unqualified names do not implicitly
search the containing object's schema. Names are folded according to the
configured name handling and retain case when quoted. Table declarations also
register their implicit composite row types, usable from other type positions.
`PgTableBuilder.ToTable(manager)` uses the same registry. Its parameterless Core
override remains an explicit stub because construction requires that context.

Identifier canonicalization is shared with Core's `ComponentNormalizer` rather
than implemented by the type resolver. Its three-argument constructor accepts
`NameHandling`; the original two-argument constructor retains `NameHandling.None`
for existing callers. Typed catalog/schema/object/column normalization includes
enclosing scopes and preserves source/provenance metadata. Case handling uses
original quoting before applying output quoting, is idempotent, and prevents
quote-stripping policies from changing a case-sensitive identifier's identity.
Sigil-prefixed identifiers retain their existing handling. PostgreSQL builders
share a configured normalizer with the registry; lookup and dependency validation
remain in `PgTypeResolutionManager`.

Finalization rejects unknown type references, duplicate enum labels or composite
fields, conflicting domain constraints, and cycles through domain bases,
composite fields, or table row types. Dependency validation is iterative and
supports forward references and long acyclic chains. Repeated finalization is
safe, and a missing declaration can be added before retrying. No partially
resolved definition is returned on failure.

Missing references reuse `SqlSyntaxException.SelectIdentifierNotFound` with the
type reference's source span. Cycle diagnostics currently use
`InvalidOperationException`, with the cycle path in the message and a `SourceRef`
in `Exception.Data`. Dedicated localized type/cycle diagnostics need approval;
no new error resource was introduced for this stage.

The path is a static, explicit list of schema identifiers. SQL session changes
(`SET search_path`), `$user` expansion, automatic temporary schemas, live catalog
lookup, and schema-qualified built-in aliases are not implemented. The resolver
handles types already parsed as built-ins and declared custom/row types, not
arbitrary types installed in a running PostgreSQL instance.

Routine overloads and PostgreSQL's separate routine namespaces are not modeled;
declaration uniqueness still follows the existing shared builder contract.
Routine bodies and default/check expressions are retained as parsed ASTs, not
evaluated or recursively type-resolved. Full expression normalization, collation
lookup, broader PostgreSQL routine syntax, and migration comparison remain deferred.
Table options, indexes/constraints, and unsupported column options explicitly
throw `NotImplementedException`; `CREATE TABLE AS SELECT` is rejected rather
than silently dropping the query. Temporary-table construction is rejected with
`NotSupportedException` until temporary-schema semantics can be represented.

## Statement Generation

Tables implement `IDatabaseObject<CreateTable>`, and custom types implement
`IDatabaseObject<Statement>` with the broad `ObjectType.Type` classification.
Enum, composite, and domain variants return their corresponding concrete create
statements. Routine models implement their Core statement contracts through
`StoredFunction` and `StoredProcedure`. Call `ToCreateStatement(includeSchema)`
on a model, then write the resulting statement with `ToSql()` or the formatter.

Generation retains custom-type identities, quote styles, enum-label order and
escaping, composite collations, domain defaults/constraints, and column
nullability/default variants and names. Creation modifiers such as `OR REPLACE`,
`IF NOT EXISTS`, and optional body separators belong to statement ASTs, not
canonical routine objects. Routine generation emits canonical CREATE statements
rather than reproducing those input choices. This is not a claim of full
PostgreSQL routine syntax validation.

`includeSchema` controls qualification of the declared object's name. Custom-type
references remain qualified to preserve resolved identity. When omitting the
declared schema, callers must supply the matching active schema during parsing
or construction. Tests cover definition-to-statement-to-SQL-to-definition round
trips with schema inclusion both enabled and disabled.

This generates individual statements, not an ordered deployment script. The
offline definition collector accepts forward references; execution ordering,
dependency-aware migration planning, and live PostgreSQL validation remain
deferred. No new integration tests were added for this stage.

Run the unit tests with:

```sh
dotnet test --project test/TcfOss.DatabaseManager.Postgres.Tests/TcfOss.DatabaseManager.Postgres.Tests.csproj
```