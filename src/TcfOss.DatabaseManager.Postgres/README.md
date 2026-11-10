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
Core's `DataTypeReference`: `BuiltIn` wraps a concrete `DataType`, `Named`
retains an unresolved `ObjectName`, and `Array` wraps an element reference with
declared dimensions. `ParseDataTypeReference` accepts unqualified,
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

This is not yet a complete PostgreSQL grammar. Custom type modifiers,
range/base/shell type declarations, catalog/internal and routine
pseudo-types and PostgreSQL-specific casts (`::`) are not implemented.
Routine support is limited to the body forms described below. Existing Core statement parsing is reused without
claiming full PostgreSQL syntax validation. Database communication, hosting/CLI
registration, migration generation, and integration
tests are deferred.

## Materialized Views

`CREATE MATERIALIZED VIEW` supports `IF NOT EXISTS`, an optional column list,
`USING` an access method, `WITH (...)` storage parameters, `TABLESPACE`, and
`WITH DATA` / `WITH NO DATA`. Its separate Core `CreateMaterializedView` AST
retains the query and declaration options without treating it as an ordinary
view. Nullable `WithData` distinguishes omission from either explicit clause.
Core owns the shared grammar; PostgreSQL dispatch enables it while the shared
MySQL/MariaDB parser and SQL Server reject materialized-view declarations.

Queries reuse SELECT, VALUES, WITH, and parenthesized query parsing. Storage
parameters retain their names, optional scalar values, and qualified parameter
names such as `toast.autovacuum_enabled`. Bare word values normalize to quoted
strings. TABLE query shorthand, additional storage-argument forms, expression
values, catalog-backed option/access-method validation, and query eligibility
checks are not implemented in this slice. PostgreSQL's unsupported OR REPLACE
and temporary materialized-view forms are rejected.

SQL/JSON round trips, recursive identifier formatting, formatter idempotence,
query dependencies, source spans, query-boundary comments, invalid syntax, and
provider boundaries are unit-tested. Dependency traversal follows the query,
not the target, storage parameters, access method, or tablespace. Definition
construction raises a sourced unsupported-statement error rather than dropping
the declaration. Resolved materialized-view objects, catalog loading, REFRESH,
ALTER/DROP lifecycle support, and migrations remain deferred.

## SQL-Body Context

Unquoted SQL routine bodies enable `ParseContext.NarrowSqlOnly` for their
statements. PostgreSQL rejects procedural blocks, statement labels, handler and
condition declarations, and legacy procedural cursor/loop-control commands only
while this flag is set. Core retains its broader grammar. The existing
`BeginAtomic` flag permits the opener and is cleared before parsing its contents;
`NarrowSqlOnly` remains active until body parsing finishes. Both flags restore
their previous values when the scope exits, including after parse errors.

Quoted routine bodies remain opaque. A future entry point for parsing extracted
body text must select SQL-only narrowing or procedural grammar explicitly;
leaving the flag unset does not implement the deferred PL/pgSQL grammar.

Mixed CTE/query, INSERT, UPDATE, DELETE, CALL, and RETURN bodies are unit-tested
for statement spans, terminators, identifier sources, dependencies, comments,
canonical SQL round trips, and formatter idempotence. Tests cover leading or
trailing LANGUAGE SQL, quoted semicolons/END text, empty terminators, closing
comments, and following top-level statements. Missing statement terminators/END
and nested atomic bodies are rejected. SQL cursor declarations remain accepted;
statement eligibility and language-specific execution/transaction semantics
are deferred.

## Procedure Invocation

CALL supports parenthesized positional arguments, named arguments using `=>` or
`:=`, positional arguments followed by named arguments, and a final VARIADIC
argument. Equality inside an argument is a positional boolean expression, not a
named argument. PostgreSQL rejects aggregate-only argument syntax, root FILTER,
WITHIN GROUP or OVER modifiers, positional arguments after named arguments, and
misplaced or repeated VARIADIC modifiers. Core retains its broader argument
grammar; MySQL/MariaDB preserve legacy CALL parsing and exclude VARIADIC, while
SQL Server rejects CALL.

Procedure names retain source identifiers and are not rewritten or formatted as
built-in function names. Dependency traversal distinguishes catalog-resolved
procedure targets from argument functions; unresolved targets remain unknown.
SQL/JSON round trips, recursive formatting/idempotence, argument normalization
with both flattening modes, and atomic-body comments/spans/terminators and
dependencies are unit-tested. VARIADIC also uses the shared function-argument
model, formatting, dependency traversal, and normalization.

Procedure lookup and overload resolution, argument binding/types/defaults,
OUT/INOUT semantics, transaction-control restrictions, and execution remain
deferred. Structured SQL-body restrictions are described above; PL/pgSQL bodies
remain opaque. No integration tests or database execution were added.

## Pagination And Row Locking

SELECT supports LIMIT expressions or ALL, OFFSET with optional ROW/ROWS, either
LIMIT/OFFSET order, and FETCH FIRST/NEXT with an optional count and ONLY or WITH
TIES. FETCH may stand alone or accompany OFFSET in either order. Canonical SQL
emits LIMIT before OFFSET and uses FETCH FIRST with ROWS. `Limit.PgLimit` uses a
null count for ALL; an explicit SQL NULL remains a literal expression.
`Limit.PgFetch` uses a null count for the omitted, implicit-one form.

Row-lock clauses retain FOR UPDATE, NO KEY UPDATE, SHARE, or KEY SHARE; optional
OF relation identifiers; NOWAIT or SKIP LOCKED; and multiple clauses. Input may
place locks before or after pagination; canonical output places locks last.
OF names are local relation identifiers, not additional object dependencies.

SQL/JSON round trips, recursive formatting/idempotence, normalization with both
flattening modes, select-tail expression dependencies, identifier sources, and
atomic-body spans/terminators are unit-tested. Core owns shared parsing; provider
component overrides preserve legacy MySQL/MariaDB LIMIT and basic row locking,
and SQL Server OFFSET/FETCH, while narrowing PostgreSQL-specific syntax.
PostgreSQL rejects MySQL comma LIMIT.

Lock-target lookup, lockable-query/relation checks, WITH TIES ordering and locking
restrictions, count/offset types and nonnegative values, execution semantics, and
exhaustive dialect/version validation remain deferred. No integration tests or
database-execution support were added.

## Aggregate And Window Parsing

Function calls retain aggregate `FILTER (WHERE ...)`, ordered-set
`WITHIN GROUP (ORDER BY ...)`, and named or inline OVER clauses. SELECT retains
named WINDOW definitions, including inherited specifications. Shared ORDER BY
items preserve optional `NULLS FIRST` / `NULLS LAST`, including aggregate and
window ordering.

Window specifications support ROWS, RANGE, and GROUPS frames with single or
BETWEEN bounds: UNBOUNDED PRECEDING/FOLLOWING, CURRENT ROW, and expression-based
PRECEDING/FOLLOWING offsets. Optional exclusions retain CURRENT ROW, GROUP, TIES,
or NO OTHERS. Structurally invalid bound combinations are rejected. SQL/JSON
round trips, recursive formatting/idempotence, both normalization modes,
dependencies, identifier sources, and atomic-body boundaries are unit-tested.
Window names are identifiers, not database-object dependencies.

Core owns the shared grammar. MySQL/MariaDB and SQL Server component overrides
narrow PostgreSQL-only modifiers while retaining shared ROWS/RANGE windows;
SQL Server also accepts WITHIN GROUP. Aggregate/window eligibility, named-window
lookup and inheritance rules, frame-offset typing/value checks, ordering
requirements, and exhaustive dialect/version validation remain deferred.

## Common Table Expressions

CTE bodies retain SELECT, INSERT, UPDATE, or DELETE statements, including nested
WITH and VALUES queries. Materialization hints are nullable: absent, MATERIALIZED,
and NOT MATERIALIZED remain distinct. SEARCH preserves breadth/depth-first order,
BY columns, and its generated sequence column. CYCLE preserves BY columns, mark
and path columns, and optional constant TO/DEFAULT values, including signed
numbers, typed/interval literals, and PostgreSQL string forms.

`CommonTableExpressionBody.Query` is a `Statement`. CTE result-column inference
uses SELECT projections or DML RETURNING lists and includes generated SEARCH/CYCLE
columns. DML bodies without RETURNING expose no result columns. Explicit column
lists provide output names during normalization; VALUES rows and expressions are
preserved. DML normalization is syntactic, not catalog-backed semantic resolution.
WITH dependency traversal covers both CTE bodies and their consuming SELECT/DML
statement while excluding local relation names and preserving genuine references
to same-named tables in nonrecursive bodies.

SQL/JSON output, formatting, normalization, source/comment preservation, and
atomic SQL-body boundaries are unit-tested. Core owns shared parsing; MySQL/MariaDB
and SQL Server narrow PostgreSQL-only components through overrides. Recursive
query shape/types, generated-name conflicts, CYCLE coercion/equality, DML placement
and execution semantics, and catalog-backed RETURNING wildcard expansion remain
deferred. MERGE CTE bodies require a future shared MERGE parser. No integration
tests or database-execution support were added.

## DML Parsing

The shared DML parser supports INSERT `DEFAULT VALUES`, explicit target aliases,
`OVERRIDING SYSTEM VALUE` / `OVERRIDING USER VALUE`, and `ON CONFLICT` with
`DO NOTHING` or `DO UPDATE`. Conflict targets retain index-inference expressions,
collations, operator classes and their parameters, optional index predicates,
or a named constraint. Update actions retain assignments and an optional WHERE
condition. DO UPDATE requires a target; DO NOTHING may omit it.

INSERT, UPDATE, and DELETE support RETURNING select-item lists. UPDATE FROM and
DELETE USING accept comma-separated relations and joins, reusing the shared
FROM parser. SQL/JSON output, formatting, expression dependencies, and atomic
SQL-body statement boundaries are covered by unit tests. A null `Insert.Source`
represents DEFAULT VALUES; `Update.From` holds a relation list.

Core parses the shared syntax once. `PgDmlParser` excludes SQL Server OUTPUT and
MySQL ON DUPLICATE KEY UPDATE; other providers narrow PostgreSQL-only components
through parser overrides. Exhaustive dialect-specific syntax validation, identity
override semantics, conflict-index/constraint lookup, and expression type checking
remain deferred. No database execution or migration support is implied.

## Array Types

Array type declarations are supported in columns, composite attributes, domain
base types, routine parameters/returns, and `CAST` targets. Shared Core reference
parsing handles suffixes such as `INT[]`, `TEXT[][]`, `INT[3][4]`, `mood[]`, and
the SQL-standard one-dimensional forms `INT ARRAY` and `INT ARRAY[4]`.
PostgreSQL uses bracket punctuation tokens; other lexers retain their existing
bracket-quoted identifier behavior. MySQL/MariaDB and SQL Server narrow type
reference parsing and do not accept these array declarations.

`DataTypeReference.Array` retains bracket count, optional declared lengths, and
whether the `ARRAY` keyword was used, for syntax-preserving writing/formatting.
Declared bounds are nonnegative integers up to `int.MaxValue`; malformed or
out-of-range bounds raise sourced parse errors. SQL-standard `ARRAY` supports
at most one explicitly sized bracket pair.

Resolved arrays are `PgDataType.Array(ElementType)` with `DataTypeClass.Array`.
Their canonical SQL spelling is `element_type[]`. PostgreSQL does not enforce
declared lengths or dimensionality, and these do not distinguish canonical
types; the resolver therefore discards them. Named elements use the same
search-path and quoting rules as other references. Arrays of domains and domains
over arrays retain distinct identities. Dependency checks follow array elements
to detect missing types and cycles, including implicit table row types.

SQL round-trip tests cover array-bearing definitions and routine signatures,
including forward references. Array values are still represented as ordinary
SQL string literals where applicable; array constructors, indexing, slices,
array operators, and validation of array value contents are not implemented.
No migration comparison or integration-test work was added for this stage.

## Definition Construction

`PgConfig` and `PgSchemaMapping` provide the minimal configuration contracts;
configuration loading, credentials, and provider registration are not wired up.
Callers currently supply `QuoteStyle.Ansi` and
`NameHandling.LowercaseUnlessQuoted` when creating a PostgreSQL configuration.

`PgDefinition` contains tables, custom types, functions, and procedures.
`PgTable` contains columns, an optional primary key, and name-keyed dictionaries
for check constraints (`Constraints`), foreign keys (`ForeignKeys`), unique
constraints (`UniqueConstraints`), and standalone indexes (`Indexes`). UNIQUE
constraints retain their constraint identity rather than becoming standalone
unique indexes. The two forms share key/INCLUDE normalization and Core components.
Constraint names are validated across all constraint kinds,
even though their dictionaries are separate. Column-level primary keys, unique
constraints, and checks use the same construction paths as table-level forms.
UNIQUE constraints and standalone indexes support INCLUDE payload columns;
these are normalized and validated separately from index key columns and do not
participate in uniqueness or foreign-key backing checks. Generated CREATE TABLE
includes primary keys, unique constraints, checks, and foreign keys; standalone
indexes are emitted separately.
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
Shared generated-expression and PG identity components are retained in
`PgColumn.Generated`. Identity supports ALWAYS/BY DEFAULT and standard sequence
options. Parsed identity options are ordered `PgStatementSequenceOption` variants,
preserving optional WITH/BY and NO forms, with single-option parsing and category
duplicate checks in Core. `PgTableBuilder.PreColumn.Identity` retains syntax until
the integer type is resolved. Canonical `PgIdentityOptions` then holds nonnullable
start, increment, bounds, cache, and cycle, with no `IWriteSql` implementation.
Defaults are increment 1, cache 1, and no cycle. Ascending bounds default to
1/type maximum; descending bounds to type minimum/-1. Start defaults to the
effective minimum or maximum according to direction. Construction validates
nonzero increment, positive cache, type-compatible ordered bounds, and start
within bounds, including manually assembled ASTs. Canonical options convert
back to explicit statement variants for SQL emission. Numeric interpolation uses
invariant culture in both Core SQL handlers, so option printers need no explicit
culture argument; custom `IWriteSql` and string dispatch remain unchanged.
Generated expressions support STORED/VIRTUAL, including PG 18's default VIRTUAL
mode. Unit tests cover identity syntax/defaults/bounds and generated-expression
storage, conflicts, and round-trips; sequence
name/logging options and full server-level generated-expression validation
remain deferred.
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

PostgreSQL routine parsing currently recognizes three body shapes: quoted string
definitions (`AS '...'` or `AS $tag$...$tag$`) with a `LANGUAGE` clause before
or after the literal; SQL `RETURN` bodies; and structured `BEGIN ATOMIC` blocks
with semicolon-terminated statements. Structured bodies default to SQL language
and reject an explicitly non-SQL language; a standalone `RETURN` body is allowed
only for functions. Bare Core bodies such as `AS RETURN` or a procedure's
unwrapped `SELECT` are not PostgreSQL routine syntax and are rejected.

Core owns routine-tail parsing and the `BeginAtomic` parser; PostgreSQL selects
the SQL routine-tail grammar while MySQL/MariaDB and SQL Server retain their
existing grammar. Atomic blocks are dispatched only at a routine-body boundary,
use the shared statement-list parser, and require inner statement terminators.
Their contained statement references remain visible. Quoted language bodies use
one opaque `RoutineBodyLiteral`, retaining decoded content and optional dollar
tag for reconstruction. Quoting does not affect literal-body equality and no
references are inferred from opaque text.

Create statements retain nullable ordered `PgStatementRoutineOption` lists in
`PgOptionsBeforeBody` and `PgOptionsAfterBody`. Each option emits its syntax,
preserving placement, null-input spelling, EXTERNAL SECURITY, and SET's `=`/TO
delimiter. Definition construction converts both lists into canonical
`PgRoutineOptions.Function` or `PgRoutineOptions.Procedure`, which are independent
of the body and omit syntax-only differences. Both share nonnullable language
and security plus local settings; procedures have no function-only attributes.
Functions have nonnullable volatility, strictness, parallel mode, cost, and rows.
Defaults are VOLATILE, non-strict, INVOKER, and UNSAFE. Cost defaults to 1 for
c/internal or 100 otherwise; rows defaults to 1000 for resolved set/table returns
or 0 otherwise. Cost/rows use finite positive `float` values for explicit clauses,
matching pg_proc's float4 representation rather than retaining numeric spelling.
Canonical emission omits ROWS when its stored value is 0. Definition construction
rejects SET FROM CURRENT with a sourced `DefinitionException.RoutineSettingFromCurrent`,
because its captured value depends on the execution environment. Canonical
language is normalized, including implicit SQL for structured bodies. Canonical
options convert to statement forms and do not implement `IWriteSql`. Generation emits options
before the body in a fixed order. The formatter handles both option lists;
bodies do not contain language clauses or duplicate `AS` flags. `RoutineTailMeta` preserves
the tail's source range for raw-text generation without expanding `Body.Meta`.
Dollar-quoted tokens retain tag and content and also work as SQL values in
expressions and column defaults. Options include volatility,
null-input behavior, security mode, PARALLEL, COST, ROWS, and per-routine SET,
including ordering around quoted bodies and duplicate/conflict checks. Parsed
language may be omitted for structured bodies; canonical options supply SQL.
Unit tests cover option spelling/order/placement, duplicate categories,
function/procedure restrictions, effective defaults, float4 normalization,
SET FROM CURRENT rejection, and canonical round-trips. Existing assertions have
been migrated to the ordered-list API and concrete canonical variants. Overload identity,
less common routine attributes, and broader PL/pgSQL grammar remain deferred.
Unit tests cover dollar-string lexing and values, routine body validation,
scope restoration, formatting, source ranges, and definition round-trips.
Routine fixtures use PostgreSQL body forms rather than permissive Core syntax.

`PgDefinitionBuilder.ProcessStatements` collects table, enum, composite, and
domain declarations, plus the currently supported Core routine forms, across
sources, retaining their original statements and
source references in private, strongly typed per-kind dictionaries. A private
`Declaration<TStatement>` groups type statements with their qualified names and
source references. The shared `DefinitionBuilder` owns the component normalizer
supplied by its provider. `GetAndValidateName` returns a fully normalized object
identifier while computing handles from original statement names, independently
of output quoting. A table and a custom type cannot claim the same handle.
`PgTableBuilder` receives that normalized identifier directly; finalization uses
the same identifier rather than reconstructing it through the registry.
`PgTableBuilder.AddColumn` stores `PreColumn` records with normalized full column
identifiers, defaults, nullability, and explicit `NotNull` state. Built-in array
types are canonicalized immediately; custom type references remain pending.
Duplicate and unsupported column options are checked at add time. Primary keys
update pending column nullability regardless of declaration order. Nullable
columns can still be valid FK targets when backed by an eligible unique key.
Constraints and indexes are normalized and structurally validated when added,
without retaining their original mutable statement lists. Only custom type and
FK-table lookup and referenced-column existence checks are deferred to
`ToTable(manager)`. It also checks the existence of local columns referenced by
check/index expressions; unsupported expression scopes are rejected at add time.
The builder exposes a column-only pseudo-table from its normalized columns.

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

Missing references raise `SqlSyntaxException.TypeNotFound`, carrying the requested
`TypeName`, the actual `SearchedSchemas`, and the type reference's source span.
Unqualified references report the ordered search path, including an explicit
empty list when applicable; qualified references report only the specified
schema/catalog. Cycles raise `SqlSyntaxException.CyclicTypeDependency`, carrying
the closed `Cycle` of resolved identifiers and the repeated type's declaration
source. Quoted identities are retained, and acyclic dependents are excluded from
the reported cycle. Both diagnostics use Core error resources and standard
`ObjectSourceRef` metadata, so they participate in normal sourced-error reporting
without an ad hoc `Exception.Data` payload.

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
Table options and unsupported column options explicitly
throw `NotImplementedException`; `CREATE TABLE AS SELECT` is rejected rather
than silently dropping the query. Temporary-table construction is rejected with
`NotSupportedException` until temporary-schema semantics can be represented.

## Table Constraints And Indexes

`PgPrimaryKey` represents the optional primary key. `PgTableConstraint` represents
named unique constraints, foreign keys, and checks, each stored in a typed
dictionary. Column-level primary/unique/check declarations are
canonicalized into table constraints. Names and column references use configured
case/quoting rules; duplicate named constraints, duplicate primary keys, repeated
key columns, and missing key columns are rejected. Primary-key columns become
canonically `NOT NULL`. Simple key constraints support plain column lists;
advanced index decorations and column prefix lengths are rejected explicitly.

Foreign-dialect clauses such as inline COMMENT, CLUSTERED/NONCLUSTERED,
SQL Server index options, and ON filegroup/partition-scheme placement raise
the resource-backed `SqlSyntaxException.UnsupportedDialectClause`, carrying
the dialect, offending clause, and statement source. Constraint and standalone
index validation share these checks. PostgreSQL FILLFACTOR is valid syntax but
its construction support is deferred, so it raises `NotImplementedException`
rather than a dialect syntax error. Options validation remains separate so
individual supported PostgreSQL options can be implemented later.

Foreign keys resolve referenced tables after all declarations have been collected,
including forward references. Final validation checks referenced tables/columns,
matching column counts, and a primary key, unique constraint, or eligible unique
index on the referenced column set. PostgreSQL does not require an index on the
referencing columns, and none is synthesized. Partial or expression unique
indexes do not serve as backing keys. Referential actions are retained. The
current grammar requires explicit referenced-column lists; omitted target lists,
column-level `REFERENCES`, deferrability, and `MATCH` clauses remain deferred.
Cross-type/operator compatibility is not validated without catalog information.

Standalone `CREATE INDEX` and `CREATE UNIQUE INDEX` statements are queued so they
can precede their tables. `PgIndex` preserves names, key parts, unique behavior,
access method, optional predicates, and INCLUDE payload columns. Missing tables/columns and relation-name
collisions are rejected, including names occupied by explicit primary/unique
backing indexes. Constraints and indexes require names, except that a primary key
may be unnamed; its generated backing-index name is not predicted. Unique indexes currently require B-tree; explicit
key ordering requires B-tree. Basic expression indexes and local-column predicates
are supported without expression normalization. INCLUDE columns are validated
separately and do not participate in uniqueness or FK backing checks. Operator classes,
storage options, concurrent creation, and anonymous index declarations are not
implemented.

CHECK/index expressions validate simple column references and references qualified
by their own table/schema/catalog. Composite-field or external-table references
are explicitly unimplemented. Full expression type/operator, subquery, and function
volatility validation remain deferred. Existing sourced definition/syntax errors
report structural failures; no new error resources were added for this stage.

Shared Core DDL parsing recognizes `USING` before ON and PostgreSQL's placement
after the table name. The parsed `IndexMethodAfterTableName` flag preserves the
syntax choice; MySQL/MariaDB and SQL Server narrow the PostgreSQL placement.
Canonical PostgreSQL indexes always emit `CREATE INDEX ... ON table USING method`.

## Statement Generation

Tables implement `IDatabaseObject<CreateTable>`, and custom types implement
`IDatabaseObject<Statement>` with the broad `ObjectType.Type` classification.
Enum, composite, and domain variants return their corresponding concrete create
statements. Routine models implement their Core statement contracts through
`StoredFunction` and `StoredProcedure`. Call `ToCreateStatement(includeSchema)`
on a model, then write the resulting statement with `ToSql()` or the formatter.
`PgTable.ToCreateStatement` includes its constraints but not standalone indexes;
use `ToCreateIndexStatements(includeSchema)` to generate those separately.

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