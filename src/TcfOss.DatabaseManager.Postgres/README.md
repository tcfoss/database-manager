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
statements. PostgreSQL `CREATE TYPE ... AS RANGE (...)` declarations retain the
subtype and optional operator class, collation, canonical function, subtype
difference function, and multirange type name in `CreateType.PgRange`. Range
syntax is gated by the PostgreSQL grammar. Plain Core `Parser`/`GenericLexer`
callers can obtain the shared enum/composite ASTs without the PostgreSQL
provider. Composite fields retain parsed type references and optional
collations; enum labels retain their order. Empty enum/composite definitions
are supported. Parsing does not check type existence; definition construction
resolves supported enum/composite references after all declarations have been
collected. Range type definition construction remains unsupported.

PostgreSQL shell declarations (`CREATE TYPE name`) and base declarations
(`CREATE TYPE name (...)`) produce `CreateType.PgShell` and `CreateType.PgBase`.
Base declarations require INPUT and OUTPUT and retain ordered options for
RECEIVE, SEND, TYPMOD_IN/OUT, ANALYZE (including ANALYSE), SUBSCRIPT,
INTERNALLENGTH, PASSEDBYVALUE, ALIGNMENT, STORAGE, LIKE, CATEGORY, PREFERRED,
DEFAULT, ELEMENT, DELIMITER, and COLLATABLE. Function names, type references,
literal values, and boolean flag forms remain distinct in the AST. Option names
follow PostgreSQL identifier case rules; unknown and duplicate options are
rejected. These declarations support SQL/JSON round trips and formatting without
looking up their references. Base/shell definition construction, shell replacement,
support-function validation, and representation/eligibility checks remain
unsupported.

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
retains an unresolved `ObjectName`, `PgPseudoType` retains PostgreSQL pseudo-type
names, `PgTypeOf` retains a PostgreSQL `%TYPE` reference, and `Array` wraps an
element reference with declared dimensions.
`ParseDataTypeReference` accepts unqualified, qualified, and double-quoted
names, preserving spelling, quoting, and source locations. `%TYPE` is accepted
only in PostgreSQL routine definitions and remains unresolved. `ParseDataType`
remains the built-in-only entry point. Invalid built-in syntax is not retried as
a named reference.

Routine signatures recognize pseudo-types in definitions and in COMMENT, ALTER,
and DROP targets. Definition construction preserves matching polymorphic output
returns and accepts polymorphic variadic types (`ANY`, `ANYARRAY`, and
`ANYCOMPATIBLEARRAY`); language eligibility and polymorphic relationships remain
deferred.

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

This is not yet a complete PostgreSQL grammar. Custom type modifiers
and some catalog/internal types are not implemented.
Routine support is limited to the body forms described below. Existing Core statement parsing is reused without
claiming full PostgreSQL syntax validation. Database communication, hosting/CLI
registration, migration generation, and integration
tests are deferred.

## Table Persistence And Temporary Lifecycle

`CREATE [TEMP | TEMPORARY | UNLOGGED] TABLE [IF NOT EXISTS] ...` preserves
temporary spelling in `CreateTable.Temporary` and PostgreSQL-specific options
in nullable `PgCreateTableOptions`. Its required UNLOGGED flag is independent
of optional `PgOnCommit`, which retains PRESERVE ROWS, DELETE ROWS, or DROP.
Omitting ON COMMIT remains distinct from writing PostgreSQL's PRESERVE ROWS
default. Existing CTAS parsing composes with ON COMMIT before AS SELECT.

Core owns the grammar. MySQL/MariaDB retain TEMPORARY TABLE but reject the new
PostgreSQL forms; SQL Server also rejects them. SQL/JSON round trips, identifier
formatting/idempotence, source/comment/statement boundaries, atomic SQL bodies,
CTAS query references and caller scopes, and malformed/deferred forms are tested.
Traditional INHERITS, LIKE/copy options, deprecated GLOBAL/LOCAL qualifiers, and
expanded CTAS variants remain deferred. Partition declarations are described
below, followed by partition attachment/detachment.

Parsing does not validate temporary-only ON COMMIT eligibility, schema/lifetime
rules, crash recovery, replication, or execution. Temporary tables and CTAS keep
their existing sourced construction errors. PostgreSQL creation options on
persistent tables are explicitly rejected until the resolved model retains them.

## Partition Declarations

`CREATE TABLE ... PARTITION BY RANGE/LIST/HASH (...)` retains a typed
`PgPartitionBy` in `PgCreateTableOptions`, with ordered column/function/
parenthesized-expression keys, optional COLLATE, and named operator classes.
`CREATE TABLE ... PARTITION OF parent ...` retains `PgPartitionOf`, including
list IN bounds, range FROM/TO bounds, hash MODULUS/REMAINDER bounds, or DEFAULT.
Range MINVALUE/MAXVALUE sentinels are distinct from expressions and quoted
strings. Hash field order is preserved.

Child declarations support inherited column overrides, optional WITH OPTIONS,
existing column options/table constraints, and a following PARTITION BY for
subpartitioning. Explicit empty body parentheses are retained. SQL/JSON round
trips, identifier formatting/idempotence, source/comment/statement boundaries,
atomic SQL bodies, typed payloads, malformed forms, and provider restrictions
are tested. PostgreSQL rejects partition declarations combined with CTAS;
existing shared CTAS behavior remains available in Core.

Dependencies include the parent table and expression references under isolated
contexts, preserving caller filters and scopes during partial enumeration.
Collation/operator-class labels are not treated as independently resolved
objects. Definition construction remains explicitly rejected with a sourced
error. Parent lookup, inherited definitions, bound arity/overlap, key/bound
eligibility, expression typing/immutability, catalog loading, migrations, and
execution remain separate. Partition lifecycle parsing is described below.

## Partition Lifecycle

`ALTER TABLE [IF EXISTS] parent ATTACH PARTITION child ...` retains a typed
`AlterTableOperation.PgAttachPartition`, reusing the declaration parser and
`PgPartitionBound` for list, range, hash, and DEFAULT bounds. Range sentinels
and hash field order keep their declaration behavior. Both table names retain
qualification, quoting, and sources, with identifier-aware formatting.

`ALTER TABLE [IF EXISTS] parent DETACH PARTITION child [CONCURRENTLY | FINALIZE]`
retains `AlterTableOperation.PgDetachPartition` with nullable
`PgDetachPartitionMode`. Omission remains distinct from either written mode.
PostgreSQL rejects ONLY and comma-combined actions for both lifecycle forms.
Core owns the grammar under the existing table-alter capability; MySQL/MariaDB
and SQL Server reject these PostgreSQL forms.

Dependencies include the target and attached/detached existing table, plus
ATTACH bound expression references under an isolated context. Caller filters
and scopes survive full and partial enumeration without capturing caller
columns or local variables. Unresolved references remain unknown. SQL/JSON
round trips, formatting/idempotence, typed payloads, comments/source spans,
statement/atomic boundaries, malformed/standalone combinations, provider
distinctions, and sourced unsupported construction are unit-tested.

Parsing does not resolve tables, partition membership, strategy/key compatibility,
bound arity/overlap, column/constraint compatibility, or existing data. It does
not enforce CONCURRENTLY's transaction/default-partition restrictions or
FINALIZE's pending-detach prerequisite; acceptance in an atomic body establishes
syntax, not executability. Index/trigger effects, privileges, locking, catalog
loading, definition mutation, migration generation, and execution remain
separate. PostgreSQL definition construction still explicitly rejects these
ALTER statements with sourced errors.

## Expanded Index Declarations

`CREATE [UNIQUE] INDEX [CONCURRENTLY] [[IF NOT EXISTS] name] ON [ONLY] table`
reuses `CreateIndex.Standard` and `CreateIndex.Unique`. Omitted index names remain
null; IF NOT EXISTS still requires a name. `IndexMethod.BuiltIn` wraps existing
`BuiltInIndexMethod` values, while `IndexMethod.Custom` retains quoted/custom
access-method identifiers. Nullable `PgCreateIndexOptions` retains other written
prefix/trailing options. PostgreSQL permits USING only after the target name.
MySQL resolved objects retain `BuiltInIndexMethod`; their builders explicitly
reject custom methods with a sourced dialect exception.

Key parts retain columns or existing expression ASTs, with COLLATE expressions,
qualified operator classes, ordered scalar operator-class parameters, explicit
ASC/DESC, and optional NULLS FIRST/LAST. Nullable `PgIndexKeyOptions` retains the
new decorations without synthesizing omitted defaults. Bare function calls and
parenthesized expressions are accepted; unparenthesized arithmetic/casts and
window-function syntax are rejected. Numeric-argument function calls are not
misclassified as MySQL column-prefix lengths.

Trailing clauses follow PostgreSQL order: INCLUDE, NULLS [NOT] DISTINCT, WITH
storage parameters, TABLESPACE, then WHERE. Index storage parameters share the
`StatementIndexOption` hierarchy with SQL Server options. `FillFactor` is shared;
PostgreSQL variants retain buffering, deduplication, fast update, GIN pending-list
limits, autosummarization, and pages per range. Known parameters are gated by
their built-in access method, treating an omitted method as BTREE without
synthesizing it in the AST. Known but ineligible parameters do not fall through
to the generic `StorageParameter` variant. Generic and operator-class parameters
reuse `PgStorageParameter`; bare word values normalize to quoted strings.
Typed option names emit uppercase, and typed booleans emit ON/OFF. Written NULLS
DISTINCT remains distinct from omission.

Core owns the shared grammar. The owning parser's `Grammar` mask enables all
dialects in Core; provider parsers select My, Ms, or Pg. Index clauses and options
are gated before consuming their tokens, keeping unsupported fields absent and
diagnostics at the offending clause rather than validating a completed index.

SQL/JSON round trips, recursive identifier formatting/idempotence, typed options,
quoted names, source/comment/statement boundaries, atomic SQL bodies, malformed
and foreign-dialect forms, and provider restrictions are unit-tested. References
include the indexed table or view and expression/predicate object dependencies.
Isolated contexts avoid capturing caller variables/columns and preserve filters
and partial-enumeration scopes. Access methods, collations, operator classes,
parameter labels, and tablespaces do not become independent object dependencies.

Method/operator-class/collation lookup, catalog-dependent option eligibility,
expression typing and immutability, index suitability, partition behavior, privileges, CONCURRENTLY
transaction restrictions, locking, catalog loading, migrations, and execution
remain separate. Atomic-body acceptance establishes syntax, not executability.
Existing supported named indexes still construct; unnamed indexes and the new
PostgreSQL index/key components raise a sourced `DefinitionException` rather
than silently dropping their meaning. ALTER/DROP INDEX extensions remain
separate work.

## Constraint Declarations And Lifecycle

Table and inline constraints retain written DEFERRABLE/NOT DEFERRABLE and
INITIALLY DEFERRED/IMMEDIATE options in nullable lists of typed
`PgConstraintOption` entries, preserving their written order. Omitted
attributes remain absent. UNIQUE directly retains NULLS DISTINCT/NULLS NOT DISTINCT
before its key list; CHECK retains NO INHERIT. Inline REFERENCES retains its
target, optional column list, referential actions, and deferrability options.

`EXCLUDE [USING method] (... WITH operator, ...)` reuses index keys, expression
ASTs, and operator references, including `OPERATOR(schema.symbol)`. Exclusion
keys support parenthesized expressions, COLLATE, qualified operator classes and
scalar parameters, direction, and null ordering. INCLUDE, storage parameters,
USING INDEX TABLESPACE, and a parenthesized WHERE predicate are retained.
Storage parameters and the index tablespace have a separate
`PgConstraintIndexParameters` component with complete SQL and formatting output.
Its storage parameters use the same `StatementIndexOption` variants and parser
as standalone CREATE INDEX, with the selected access method passed through.
Methods remain nullable rather than defaulting to BTREE or GIST.

ALTER TABLE reuses existing ADD operations, with NOT VALID accepted after a
constraint declaration. DROP CONSTRAINT retains IF EXISTS and CASCADE/RESTRICT.
RENAME CONSTRAINT is standalone, while ALTER CONSTRAINT, VALIDATE CONSTRAINT,
and ADD PRIMARY KEY/UNIQUE USING INDEX support the applicable action lists.
ONLY and table-level IF EXISTS retain their existing behavior. Core owns shared
grammar; MySQL/MariaDB and SQL Server reject the new PostgreSQL forms.

SQL/JSON round trips, recursive identifier formatting/idempotence, typed payloads,
quoted names, sources/comments, statement boundaries, atomic SQL bodies,
malformed syntax, provider restrictions, and sourced unsupported construction
are unit-tested. Dependencies include referenced foreign tables and functions
in CHECK/exclusion expressions, not constraint labels, operators, operator
classes, or tablespaces. Traversal preserves caller filters and scopes without
capturing caller columns or local variables.

New constraint options/exclusions/inline references and lifecycle statements
raise sourced `DefinitionException` errors until resolved definitions retain
their meaning. Existing supported plain constraints remain supported. Parsing
does not establish constraint eligibility, operator/index suitability, catalog
existence, expression typing, privileges, validation results, locking behavior,
or transaction executability. Catalog loading, definition mutation, migration
generation, and execution remain separate. Temporal WITHOUT OVERLAPS/PERIOD,
ENFORCED/NOT ENFORCED, table-level NOT NULL constraints, and further foreign-key
clause extensions are outside this slice.

## Sequences And Identity Alterations

`CREATE [TEMP | TEMPORARY | UNLOGGED] SEQUENCE [IF NOT EXISTS] name ...`
produces Core's `CreateSequence`, retaining persistence spelling and ordered
`PgStatementSequenceOption` nodes. Options cover AS SMALLINT/INTEGER/BIGINT,
START [WITH], INCREMENT [BY], MINVALUE/MAXVALUE and their NO forms, CACHE,
CYCLE/NO CYCLE, and OWNED BY a qualified column or NONE. INT/INT2/INT4/INT8 type
aliases normalize to canonical spellings. The numeric option parser is shared
with existing identity-column declarations through `ComponentParser`.

`AlterSequence` retains IF EXISTS and a typed `PgAlterSequenceOperation`:
ordered parameter changes (including RESTART with an optional value), OWNER TO,
RENAME TO, SET SCHEMA, and SET LOGGED/UNLOGGED. Owner specifications reuse
`Account`, including CURRENT_ROLE, CURRENT_USER, SESSION_USER, and quoted names.
`DROP SEQUENCE` reuses `DropObject` with multiple targets, IF EXISTS, and optional
CASCADE/RESTRICT. Ordinary-view and materialized-view syntax remains separate.

`ALTER TABLE [IF EXISTS] [ONLY] name ALTER [COLUMN] column ...` supports ADD
GENERATED ALWAYS/BY DEFAULT AS IDENTITY with optional sequence options, SET
GENERATED, RESTART, SET numeric/bounds/cache/cycle options, and DROP IDENTITY
[IF EXISTS]. Ordered grouped SET/RESTART changes and comma-separated column
alterations retain their structure. Core owns the grammar; the MySQL/MariaDB
and SQL Server parsers retain their previous rejection of sequence CREATE/ALTER
and PostgreSQL identity forms rather than acquiring them through shared dispatch.
SQL Server accepts its native DROP SEQUENCE syntax, including IF EXISTS and
multiple names, but not CASCADE/RESTRICT.

SQL/JSON round trips, formatting, identifier sources, comments and statement
boundaries, SQL atomic bodies, signed 64-bit bounds, and provider distinctions
are unit-tested. Dependency traversal reports existing ALTER/DROP targets and
OWNED BY table references, not newly declared sequence names, owner roles, or
rename/schema destinations; sequence targets remain unknown until resolution
exists. Dependency filters and caller scopes are preserved.

This completes the bounded sequence/identity ALTER parsing milestone, not
sequence execution or semantic validation. Increment/cache validity, bound and
type compatibility, ownership/schema restrictions, identity eligibility,
temporary lifetimes, privileges, and locking are not resolved. Schema-qualified
or quoted sequence type names and alternate numeric literal spellings remain
grammar extensions. Definition construction explicitly rejects sequence
lifecycle and identity ALTER statements with sourced diagnostics. Resolved
sequences, implicit serial/identity sequence construction, catalog loading,
migrations, and execution remain deferred.

## Column Structural And Configuration Alterations

`ALTER TABLE [IF EXISTS] [ONLY] name ...` supports ADD [COLUMN] [IF NOT EXISTS],
DROP [COLUMN] [IF EXISTS] with optional CASCADE/RESTRICT, and standalone
RENAME [COLUMN] old TO new. Optional COLUMN spelling, existence flags, and
omitted versus explicit drop behavior survive SQL/JSON round trips.

`ALTER [COLUMN] name ...` supports TYPE and SET DATA TYPE with optional COLLATE
and USING expressions, SET/DROP DEFAULT, SET/DROP NOT NULL, SET EXPRESSION AS
(expression), and DROP EXPRESSION [IF EXISTS]. Defaults and USING/generated
expressions reuse the existing expression grammar. Ordered comma-separated
actions compose with the existing identity alterations.

`Parser.SupportsDefaultExpressionsWithoutParentheses` controls full expression
defaults in both column declarations and ALTER operations. It is enabled in Core,
PostgreSQL, and SQL Server; the shared MySQL/MariaDB parser retains its existing
default parsing. Literal defaults keep their literal AST variant. Declaration
defaults stop before a trailing column COLLATE clause, while ALTER defaults can
include expression COLLATE directly.

Column configuration covers SET STATISTICS integer/DEFAULT, SET/RESET option
lists, SET STORAGE PLAIN/EXTERNAL/EXTENDED/MAIN, and SET COMPRESSION name/DEFAULT.
Core's shared `PgColumnConfigurationOperation` replaces the materialized-view-only
component and is reused by both table and materialized-view column alterations.
Option assignments require scalar values; quoted compression names remain
distinct from DEFAULT. Core owns the grammar; MySQL/MariaDB and SQL Server retain
their existing implemented ALTER forms without inheriting these PostgreSQL
extensions. PostgreSQL rejects MySQL MODIFY/CHANGE, FIRST/AFTER positions,
AUTO_INCREMENT, and character-set alterations.

SQL/JSON round trips, typed payloads, formatting/idempotence, sources/comments,
statement/atomic boundaries, malformed clauses, provider distinctions, and sourced
unsupported construction are unit-tested. Dependency traversal reports the
existing table, written named/array element types and collations, and references
within defaults/generated/USING expressions. Types and collations remain
unresolved; configuration names and rename destinations are not dependencies.
Filters and caller scopes are preserved, including partial enumeration.

This completes the bounded structural/configuration column parsing slice.
Type conversion, column eligibility, option names/values and statistics limits,
dependency/cascade effects, catalog updates, migration generation, and execution
remain deferred. PostgreSQL definition construction explicitly rejects these
ALTER statements. Table-level/dependency-sensitive ALTER and broader routine
ALTER forms are separate follow-up work.

## Table Management And Dependency Links

The first bounded table-level ALTER slice supports OWNER TO, SET SCHEMA,
SET TABLESPACE, SET ACCESS METHOD name/DEFAULT, CLUSTER ON, SET WITHOUT CLUSTER,
storage-parameter SET/RESET, SET LOGGED/UNLOGGED, INHERIT/NO INHERIT, and OF/NOT OF.
Owner specifications retain named roles and CURRENT_ROLE/CURRENT_USER/SESSION_USER;
quoted access-method names remain distinct from DEFAULT. Storage parameters reuse
the existing scalar-value grammar, including optional values and toast-qualified
names. SET SCHEMA is standalone and rejects ONLY; the other new actions compose
with column alterations. Table rename also rejects ONLY.

Core owns the grammar. `PgAlterRelationOperation` generalizes the former
materialized-view operation component, and `DdlParser.ParseOptionalAlterRelationOperation`
shares ownership, placement, access-method, storage, and clustering parsing.
Table-specific persistence, inheritance, and composite-type links remain distinct
`AlterTableOperation` variants. `SupportsPostgresTableAlterSyntax` narrows these
forms out of the MySQL/MariaDB and SQL Server parsers without changing their
existing ALTER syntax or statement spelling preservation. Materialized views do
not acquire table-specific persistence or inheritance syntax.

Dependency traversal reports written INHERIT/NO INHERIT parents as table references
and OF types as unresolved references, preserving filters and caller scopes even
when enumeration stops early. Owner roles, destination schemas/tablespaces,
storage labels, access methods, and table-local clustering index names are not
reported as independent database objects. SQL/JSON round trips, formatting,
sources/comments, statement/atomic boundaries, malformed/standalone clauses,
provider distinctions, and sourced unsupported construction are unit-tested.

This does not complete the table-level ALTER roadmap item. Bulk tablespace moves,
explicit descendant `*` spelling, rule states, legacy SET WITHOUT OIDS, and
expanded constraint operations remain pending. Partition lifecycle parsing is
implemented in its own slice above. Parent/type/role lookup, compatibility,
privileges, physical rewrites, catalog updates, migration generation, and execution are deferred;
PostgreSQL definition construction still explicitly rejects ALTER statements.

## Table Security And Replication State

ALTER TABLE supports ENABLE/DISABLE ROW LEVEL SECURITY and FORCE/NO FORCE ROW
LEVEL SECURITY, trigger ENABLE/DISABLE for named triggers or ALL/USER selectors,
ENABLE ALWAYS/REPLICA for named triggers, and REPLICA IDENTITY
DEFAULT/FULL/NOTHING/USING INDEX. All compose with existing combinable actions,
including under IF EXISTS and ONLY; standalone rename/SET SCHEMA restrictions
still apply.

Core owns the parsing under `SupportsPostgresTableAlterSyntax`; MySQL/MariaDB and
SQL Server retain their previous implemented syntax. Typed `AlterTableOperation`
variants use `PgRowSecurityMode`, `PgTriggerStateMode`, `PgReplicaIdentityMode`,
and `PgAlterTriggerTarget`. ALL/USER selectors remain distinct from quoted trigger
names, including names with those spellings. ALWAYS/REPLICA reject the unquoted
selectors. Named triggers and replica-identity indexes are single identifiers,
preserving quoting and sources and using identifier-aware formatting.

Reference traversal retains the existing target-table behavior; trigger names
and table-local index labels do not invent independent object dependencies or
inherit colliding pseudo-table/local-variable classifications. SQL/JSON round
trips, formatting/idempotence, typed payloads, comments/source spans, following
statements, SQL atomic bodies, mixed operations, filters/partial-enumeration
scopes, malformed/deferred forms, provider distinctions, and sourced unsupported
construction are unit-tested.

This slice does not add policy declarations, resolve triggers/indexes, validate
index suitability or privileges, apply table-state changes during definition
construction, or implement replication behavior, catalog loading, migrations,
or execution. Parsing a command inside an atomic body does not establish its
executability. Rule controls and legacy administration remain deferred;
constraints remain a separate slice, and partition lifecycle parsing is
described above.

## Shared DROP Options And Type/Domain Targets

Core's `DropObject` supports TYPE and DOMAIN targets with optional IF EXISTS,
multiple undecorated names, and CASCADE/RESTRICT. Omitted behavior remains null
rather than becoming an explicit RESTRICT. Names preserve quoting and sources;
TYPE/DOMAIN dependencies remain unresolved object references, not table/view,
column, or local-variable references.

PostgreSQL also accepts target lists and behavior suffixes for TABLE, VIEW,
MATERIALIZED VIEW, SEQUENCE, INDEX, and SCHEMA. Provider hooks narrow target
support, list cardinality, TEMPORARY, and optional behavior. PostgreSQL rejects
DROP TEMPORARY and MySQL/SQL Server's DROP INDEX ... ON syntax. SQL Server accepts
single-target DROP TYPE and multi-target DROP SEQUENCE without behavior suffixes.
MySQL/MariaDB reject TYPE/DOMAIN and retain their required INDEX ... ON form;
TABLE/VIEW compatibility CASCADE/RESTRICT suffixes are accepted and preserved,
without implying PostgreSQL cascade semantics.

SQL/JSON round trips, formatting, identifier sources, comments, statement/atomic
boundaries, dependency filters/scopes, and provider distinctions are unit-tested.
This is bounded parsing support: decorated DROP TYPE names,
DROP INDEX CONCURRENTLY, dependency resolution,
cascade effects, catalog updates, migration generation, and execution remain
deferred. PostgreSQL definition construction explicitly rejects these DROP
statements with sourced diagnostics.

## Routine DROP Signatures And Trigger Targets

`DROP FUNCTION`, `DROP PROCEDURE`, and `DROP ROUTINE` produce Core's `DropRoutine`
with ordered `PgRoutineReference` targets, IF EXISTS, and optional
CASCADE/RESTRICT. Each target has nullable parameters: null preserves an omitted
signature, while an empty list preserves `()`. Arguments reuse
`StatementRoutineParameter` and `DataTypeReference`, retaining optional names and
IN/OUT/INOUT/VARIADIC modes, named and array types, and identifier sources.
Defaults and SQL Server parameter options are not accepted. Existing type alias
normalization applies. Core parses this syntax once; MySQL/MariaDB and SQL Server
reject signatures and ROUTINE while retaining their native simple FUNCTION and
PROCEDURE `DropObject` statements. `DdlParser.ParseDropObject` now returns
`Statement` to accommodate both forms.

`DROP TRIGGER [IF EXISTS] name ON table [CASCADE | RESTRICT]` reuses `DropObject`.
PostgreSQL requires ON and an unqualified trigger name through existing
trigger-name narrowing. MySQL/MariaDB and SQL Server do not acquire this
PostgreSQL table-target grammar. ON targets use identifier-aware formatting.
Dependency traversal reports unresolved routine targets and written named
signature types, not argument names or built-in types. Trigger names remain
unresolved separately from their table targets; filters and caller scopes are
preserved even when enumeration stops early.

SQL/JSON round trips, omission distinctions, formatting, comments, sources,
statement/atomic boundaries, provider restrictions, and sourced unsupported
definition construction are unit-tested. Signature parsing follows the existing
shared parameter and identifier rules, without adding stricter reserved-word
validation. Overload lookup, OUT/VARIADIC identity semantics, object existence,
dependency/cascade effects, aggregate-specific DROP syntax, SQL Server DDL
trigger targets, catalog loading, migrations, and execution remain deferred.

## Routine ALTER Forms

`ALTER FUNCTION` and `ALTER PROCEDURE` produce Core's `AlterRoutine`, with a
`PgRoutineKind`, shared `PgRoutineReference`, ordered `PgAlterRoutineOperation`
actions, and optional trailing RESTRICT for attribute/configuration lists.
Omitted signatures remain distinct from `()`. Written arguments reuse the DROP
signature parser, including modes, optional names, named/array types, and sources;
defaults are not accepted.

Functions support volatility, null-input behavior, PARALLEL, COST, ROWS, and
security attributes through the existing routine-option parser. Procedures accept
security attributes, not function-only options. Both support configuration SET
with TO/=, scalar value lists, FROM CURRENT, or DEFAULT, plus RESET name and
RESET ALL. Unquoted DEFAULT is a distinct action, not the quoted string
`'DEFAULT'`; quoted `"ALL"` remains a parameter name rather than RESET ALL.
Action order and delimiter spelling are retained. Shared SET head/value parsing
also serves CREATE without changing its existing behavior.

RENAME TO, OWNER TO, SET SCHEMA, and [NO] DEPENDS ON EXTENSION are standalone
forms, reusing the relation-operation components and narrow owner parser. They
do not combine with attributes or take trailing RESTRICT. Names use
identifier-aware formatting; destinations and owner roles do not become object
dependencies. Routine targets and written named signature types remain unresolved
references, isolated from colliding tables/local variables and preserving filters
and caller scopes even on partial enumeration.

Core owns the grammar. Existing MySQL/MariaDB and SQL Server ALTER dispatch
continues to reject these PostgreSQL forms. SQL/JSON, formatting/idempotence,
typed settings/signatures, sources/comments, statement/atomic boundaries,
dependency scopes, malformed and inapplicable actions, provider distinctions,
Core reuse, and sourced unsupported construction are unit-tested.

This bounded slice excludes generic ALTER ROUTINE, LEAKPROOF/SUPPORT and other
advanced routine attributes, body/language/return-type replacement, overload
lookup, argument identity, target/role/extension resolution, ROWS applicability,
configuration semantics, privileges, definition-state mutation, catalog loading,
migrations, and execution. Parsing inside an atomic body does not establish
executability. PostgreSQL definition construction explicitly rejects AlterRoutine.

## Ordinary Views

`CREATE [OR REPLACE] VIEW name [(columns)] WITH (options) AS query` retains an
ordered option list in nullable `CreateView.PgOptions`. It reuses Core's
`PgStorageParameter` representation for optional scalar assignments, including
`security_barrier`, `security_invoker`, and `check_option`. Omitted options remain
null; names and optional values retain their order. Bare word values normalize
to quoted strings. Known option names, value types, duplicates, conflicts, and
query eligibility are not semantically validated.

Core owns the shared grammar. MySQL/MariaDB and SQL Server reject PostgreSQL's
parenthesized options; PostgreSQL rejects SQL Server's ENCRYPTION/SCHEMABINDING
view options. `WITH [LOCAL | CASCADED] CHECK OPTION` composes with the declaration
options. `CreateView.ViewCheckOption` uses the statement variants
`ViewCheckOption.Unspecified` and `ViewCheckOption.Specified(ViewCheckOptionScope)`
to retain omitted versus explicit scopes for SQL/JSON round trips and formatting.
No clause has a null check option. Canonical MySQL/MariaDB views use nullable
`ViewCheckOptionScope`, resolving an unspecified scope to CASCADED and emitting
explicit scopes when converted back to statements.
Core also accepts this valid unqualified form for MySQL/MariaDB and SQL Server;
MySQL/MariaDB definition construction keeps the effective mode rather than the
statement spelling. PostgreSQL updateability/check-option eligibility validation
remains deferred.
Materialized-view population, access-method, and tablespace clauses do not become
ordinary-view clauses.
PostgreSQL also rejects IF NOT EXISTS, OR ALTER, and MySQL ALGORITHM/DEFINER/SQL
SECURITY declaration modifiers. Core retains its kitchen-sink IF NOT EXISTS
grammar through a provider hook. Both view kinds require query bodies rather
than arbitrary statements: SELECT, VALUES, WITH queries, and parenthesized
queries are covered, with parse diagnostics for non-query bodies.

`CREATE [OR REPLACE] [TEMP | TEMPORARY] [RECURSIVE] VIEW ...` preserves temporary
spelling in nullable `CreateView.TemporaryLabel` and recursion in `Recursive`.
Recursive views require a nonempty column list and do not accept a trailing
CHECK OPTION clause. Their implicit unqualified self-reference is handled through
the existing recursive CTE dependency traversal; external and schema-qualified
references remain dependencies. The statement body is not rewritten into a CTE.
MySQL/MariaDB and SQL Server reject temporary and recursive view declarations.
TEMPORARY TABLE remains supported in MySQL/MariaDB, and Core/PostgreSQL temporary
table statements also retain TEMP versus TEMPORARY spelling.

SQL/JSON round trips, ordered fields, identifier sources, statement boundaries,
query-only dependencies, recursive identifier formatting, and formatter
idempotence are unit-tested. A pre-existing shared nested-query formatter issue
can duplicate comments trailing a bare query; comment idempotence here covers
the trailing CHECK OPTION boundary instead. The bounded ordinary-view declaration
parsing milestone is complete. Shared query grammar extensions (including TABLE
shorthand), expanded scalar argument forms, and resolved view construction remain
follow-ups.
Recursive-query eligibility, temporary-object lifetime/schema restrictions, and
automatic temporariness from referenced objects are not semantically resolved.
Definition construction explicitly rejects ordinary views rather than discarding
their options. Catalog loading, migrations, and execution remain deferred.

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
the declaration.

`REFRESH MATERIALIZED VIEW [CONCURRENTLY] name [WITH [NO] DATA]` produces a
separate Core `RefreshMaterializedView` AST. It retains concurrency and omitted
versus explicit population clauses, and rejects `CONCURRENTLY` with
`WITH NO DATA`. MySQL/MariaDB and SQL Server reject refresh statements.
SQL/JSON round trips, quoted identifier formatting, formatter idempotence,
statement sources/comments/terminators, atomic SQL bodies, and dependency
filters are unit-tested. Dependency traversal reports the target as unknown
and isolates it from enclosing column/variable contexts; it does not pretend
that the target has been resolved as an ordinary view or table.

`DROP MATERIALIZED VIEW [IF EXISTS] name [, ...] [CASCADE | RESTRICT]` reuses
Core's `DropObject` with a distinct `DroppableObject.MaterializedView` target
kind. Nullable `PgBehavior` retains omission versus explicit RESTRICT; multiple
targets keep their order and quoting. MySQL/MariaDB and SQL Server reject this
target through the shared DROP parser's provider hook. TEMPORARY, CONCURRENTLY,
and repeated/conflicting behaviors are rejected. SQL/JSON round trips,
formatting/idempotence, source/comment/terminator boundaries, atomic SQL bodies,
and dependency filters are unit-tested. A separate reference context keeps
unresolved materialized-view targets distinct from ordinary views and local
variables. Behavior suffixes on other DROP targets remain outside this slice.

`ALTER MATERIALIZED VIEW [IF EXISTS] name` supports `RENAME TO`, column renaming
with or without the COLUMN keyword, `SET SCHEMA`, `SET TABLESPACE`,
storage-parameter `SET (...)` / `RESET (...)`, `OWNER TO`,
`SET ACCESS METHOD`, `CLUSTER ON`, and `SET WITHOUT CLUSTER`. A separate
`AlterMaterializedView` statement with ordered `Operations` and typed
`PgAlterMaterializedViewOperation` variants retains each form and its identifier
fields; the single-action constructor remains available. Core owns parsing;
MySQL/MariaDB and SQL Server reject these statements
without changing their existing ALTER TABLE behavior. SQL/JSON round trips,
recursive identifier formatting/idempotence, sources/comments/terminators,
atomic SQL bodies, and dependency filters are unit-tested. Dependencies report
only the unresolved target, not destination names, in an isolated context.

Storage SET and RESET retain parameter order and one- or two-part names,
including `toast.autovacuum_enabled`. SET shares CREATE's optional scalar-value
model, preserving omitted values and normalizing bare words to quoted strings.
RESET accepts names only. Lists must be nonempty; expression values, additional
storage-argument forms, and RESET assignments are rejected. Storage names are
not object dependencies. SQL/JSON, formatting, source boundaries, atomic bodies,
provider rejection, and sourced builder rejection are unit-tested. Parameter
applicability, duplicates, and value types/ranges are not catalog-validated.

`OWNER TO` accepts an explicit role identifier, `CURRENT_ROLE`, `CURRENT_USER`,
or `SESSION_USER`. It reuses Core's `Account` variants to distinguish named
roles from special roles: `"CURRENT_USER"` remains an `Account.Identity` with
identifier source metadata preserved through `ExtendedIdentifier` conversion.
PostgreSQL role parsing stays narrower than shared account parsing. SQL/JSON
round trips, quoted/special-role formatting and idempotence,
sources/comments/terminators, atomic bodies, target-only dependencies, provider
rejection, and sourced builder rejection are unit-tested. Role names do not add
database-object dependencies. Qualified roles, string literals, host accounts,
and special-role function-call syntax are rejected. Role lookup, ownership
checks, SET ROLE eligibility, schema privileges, and ownership execution are
not implemented.

`SET ACCESS METHOD` accepts a method identifier or DEFAULT; separate typed
operations keep DEFAULT distinct from a quoted method named `"default"`.
`CLUSTER ON` retains a single index identifier, while `SET WITHOUT CLUSTER`
clears the default clustering-index selection. Neither clustering action runs
CLUSTER. SQL/JSON round trips, identifier formatting/idempotence, sources,
comments/terminators, atomic bodies, target-only dependencies, provider rejection,
and sourced builder rejection are unit-tested. Method and index names do not add
independent object dependencies. Qualified names, string literals, missing
operands and unsupported modifiers are rejected. Method
eligibility, index existence/ownership/suitability, privileges, locking, physical
rewrites, and execution are not implemented.

Column actions support `ALTER [COLUMN] name SET STATISTICS` with a signed integer
or DEFAULT, `SET (...)` option assignments, `RESET (...)` option names,
`SET STORAGE` with PLAIN / EXTERNAL / EXTENDED / MAIN / DEFAULT, and
`SET COMPRESSION` with a method identifier or DEFAULT. Typed column operations
preserve COLUMN spelling, parameters, identifier sources, and default selections.
Option values reuse scalar storage-value parsing; expressions, qualified option
names, missing assignments, and empty lists are rejected. Statistics ranges,
option applicability, and compression eligibility remain semantic checks.

`[NO] DEPENDS ON EXTENSION name` retains the extension identifier and NO flag;
it is a standalone form without IF EXISTS. Combinable actions retain their order
in a comma-separated list. Renames, schema moves, and extension dependencies
remain standalone and are rejected inside action lists.

`ALTER MATERIALIZED VIEW ALL IN TABLESPACE source [OWNED BY role [, ...]]
SET TABLESPACE destination [NOWAIT]` produces a separate
`AlterMaterializedViewsInTablespace` statement. It retains tablespace identifiers,
optional ordered Account filters, NOWAIT, and source metadata, without inventing
individual-object dependencies. Bulk forms, column/extension actions, ordered
lists, SQL/JSON round trips, formatting/idempotence, comments/terminators, atomic
bodies, invalid syntax, provider boundaries, and sourced builder rejection are
unit-tested.

Basic standard/unique index declarations reuse `CreateIndex` with the
materialized-view target name, including supported INCLUDE/filter syntax.
PostgreSQL `DROP INDEX [IF EXISTS] name` does not accept ON; MySQL/MariaDB and
SQL Server still require their ON target. The declaration/index/refresh/drop-index
workflow is unit-tested. Expanded CREATE INDEX grammar, including CONCURRENTLY,
is described above. ALTER/DROP INDEX extensions remain separate work; TABLE query
shorthand and expanded storage-argument forms remain explicit shared follow-ups.

The bounded materialized-view lifecycle parsing milestone is complete. Target,
column and destination lookup, name collisions, privileges, dependency updates, tablespace
eligibility, and ALTER execution are not implemented. Parsing these statements
inside atomic SQL bodies does not validate their executability.

Definition construction rejects refresh, DROP, and ALTER commands with source
information. Resolved materialized-view objects, catalog loading, index resolution,
and migrations remain deferred. DROP does not check target existence,
ownership, or dependencies, or execute cascades. Refresh target lookup, unique-index
and existing-population prerequisites, privileges, locking, and execution are
not implemented. Parsing refresh commands in atomic SQL bodies does not check
whether those bodies can execute on PostgreSQL.

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
a sourced `DefinitionException.TempTable` until temporary-schema semantics can
be represented. Persistent PostgreSQL table creation options likewise raise a
sourced `DefinitionException` rather than silently losing their meaning.

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
SQL Server index options, and ON filegroup/partition-scheme placement are rejected
while parsing. Manually supplied legacy constraint/index components retain
resource-backed `SqlSyntaxException.UnsupportedDialectClause` checks, carrying
the dialect, offending clause, and statement source. Legacy constraint
`Options` containing FILLFACTOR still raise `NotImplementedException`.
Parsed constraint and standalone index storage parameters share typed
`StatementIndexOption` variants and a generic extension-parameter variant.
Their construction support remains deferred: unrepresented parameters,
including FILLFACTOR, raise a sourced `DefinitionException` rather than being
silently dropped. Syntactic eligibility and construction support remain separate.

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