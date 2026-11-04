# AI Coding Agent Instructions for DatabaseManager

This repository is a .NET/C# monorepo for a database management CLI and providers.

## Code Style

- Language: C# targeting .NET 10.
- Prefer primary constructor where possible.
- Prefer collection expressions (e.g. `List<int> list = [1, 2, 3];`) where possible.
- Use `var` when the type is obvious from the right-hand side (e.g. `var list = new List<int>();`), but use explicit types when the type is not clear (e.g. `List<int> list = GetList();`).
- Always use braces for `if`, `else`, `for`, `foreach`, and `while` statements, even if the body is a single line.
- Prefer block-bodied functions over expression-bodied functions. A method that
  simply returns a literal value may also be expression-bodied.
- Prefer expression bodies for read-only properties that simply return a literal value.

## Architecture

- `src/TcfOss.DatabaseManager.App/` owns CLI argument parsing and presentation.
  Command execution also lives in Core and provider `App/` folders; `*.Hosting`
  projects own host and dependency-injection setup.
- Core owns shared parsing, ASTs, and database-agnostic definition logic.
  MySQL, MariaDB, MSSQL, and PostgreSQL providers implement dialect-specific
  behavior in the corresponding layers. MariaDB reuses MySQL's lexer/parser.
- Keep parsed syntax in `Statements/` and `Expressions/`, resolved database
  objects in `DatabaseObjects/`, and the conversion between them in
  `DefinitionBuilding/`. Use provider folders matching Core where relevant.
- Keep shared ASTs provider-agnostic. Optional dialect-specific additions on
  shared nodes must be nullable; required fields inside a dialect-specific
  component or variant should remain nonnullable. Dialect-specific AST
  components belong in Core, with `My`, `Ma`, `Ms`, or `Pg` prefixes.
- Core parsers are **kitchen-sink**; provider sub-parsers **narrow** the accepted
  syntax through virtual hooks. Parse shared syntax in Core once. Optional
  parsers return null when the clause is absent, not simply because Core is
  dialect-agnostic. Choose hook signatures to fit the behavior rather than
  requiring every hook to return a nullable component.
- For irreconcilable dialect grammars, use virtual dispatch with provider
  implementations; Core may throw `NotSupportedException` where no shared
  interpretation exists. Reuse existing hooks and sub-parsers before adding
  new abstractions or near-empty dialect files.

See the [contributor folder guide](../docs/development/index.md)
for the Core directory layout.

## Tests

- Behavior changes require appropriate tests. Run affected tests for code
  changes; broaden validation when shared contracts or provider behavior change.
- Group related tests together, with happy paths before errors and helpers last.
  Add unrelated new groups after existing cases, before helpers.
- Keep filesystem I/O in integration tests, not unit tests. Reuse
  `FsProjectFixture` for temporary directories; see the
  [integration-test guide](../docs/development/integration-tests.md).
- Consult the user before adding integration tests.

## Build and Test

- Build solution: `dotnet build DatabaseManager.slnx`
- Build single project: `dotnet build src/TcfOss.DatabaseManager.App/TcfOss.DatabaseManager.App.csproj`
- Run all unit tests: `dotnet test solutions/UnitTests.slnx`
- Run all unit tests plus cheap integration tests: `dotnet test solutions/BasicTests.slnx`
- Run all tests (including long-running integration tests): `dotnet test DatabaseManager.slnx`
- Run a single test project: `dotnet test test/TcfOss.DatabaseManager.Core.Tests/TcfOss.DatabaseManager.Core.Tests.csproj`

Tests live under `test/`. Run affected tests when changing code.

## Terminology

Different RDBMSs use different terminology; this project standardizes as follows:

- **"Object"** / **"Database object"**: A discrete database entity (e.g. table, view, trigger, stored procedure). Does _not_ include columns or indexes, which are part of a larger object.
- **"Schema"**: Namespace/container for database objects. **Always and only** refers to this organizational level — never to the overall database structure.
- **"Catalog"**: The top-level container for schemas. In MySQL/MariaDB this is always `"def"` (MariaDB may support multiple catalogs in the future).
- **"Definition"** / **"Database definition"** / **"Database structure"**: The complete set of all database objects, including all schemas.
- **"Database"**: **Never** used as an organizational level (ambiguous — synonymous with "schema" in MySQL/MariaDB, synonymous with "catalog" in SQL Server). Use it only in the general sense of the entire database system or instance.

## Patterns

### Nested-Class Pseudo-Namespaces

Group simple related variants as nested records under their shared abstract
base, following `Core/Common/Value.cs`.

### StringEnumAttribute

Use `StringEnumAttribute` on a partial class for fixed sets of SQL string values.

- The first argument is the emitted SQL text. Omit the second argument unless
  overriding the generated PascalCase member name. Subsequent arguments are
  equivalent spellings; pass null for the second argument when supplying aliases
  without a name override.
- Prefer generated `Parse`/`TryParse` over handwritten case-insensitive lookup.
  When a dialect supports most values, parse and post-filter; a custom switch
  is acceptable when it supports only a small subset.

See the [generator README](https://github.com/tcfoss/string-enum-generator#readme)
for its API and examples.

## Working Guidelines

- **Providers**: Changes that affect SQL generation or communication must be validated against both `src/TcfOss.DatabaseManager.MySql` and `src/TcfOss.DatabaseManager.MariaDb`.
- **Security**: Changes to `DatabaseComms` or any code that builds or executes SQL must be reviewed for injection risks—match existing escaping/parameterization patterns.
- **NuGet**: The repository-level `nuget.config` controls feeds—do not hardcode external package sources.
- **Human review**: Ask before making large refactors of `TcfOss.DatabaseManager.Core`, changes to CLI behavior in `Program.cs`/`CommandLineInterface.cs`, or changes to release/deployment scripts under `tools/`.
- **Build configuration**: Ask before overriding repository-wide settings or analyzers in `Directory.Build.props` and `Directory.Build.targets`.
- **Errors**: Reuse resource-backed diagnostics in `Core/Resources/ErrorMessages.resx`; consult the user before adding new application-specific errors.
- **git**: A human will execute all `git add` and `git commit` commands. Agents may suggest commits and commit messages but must not execute them directly.
- **Package-local guidance**: This file is the workspace-wide default.


## Challenge Questionable Approaches

Raise concrete risks, convention conflicts, or clearly better alternatives before
implementing; do not silently override instructions or manufacture objections.
Push back at most twice per decision, with new evidence on the second occasion.
Once the user acknowledges the tradeoff and reaffirms his/her choice, proceed.
