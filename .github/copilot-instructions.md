# AI Coding Agent Instructions for DatabaseManager

This repository is a .NET/C# monorepo for a database management CLI and providers.

## Code Style

- Language: C# targeting .NET 10.
- Prefer primary constructor where possible.
- Prefer collection initializers (e.g. `List<int> list = [1, 2, 3];`) where possible.
- Use `var` when the type is obvious from the right-hand side (e.g. `var list = new List<int>();`), but use explicit types when the type is not clear (e.g. `List<int> list = GetList();`).
- Always use braces for `if`, `else`, `for`, `foreach`, and `while` statements, even if the body is a single line.

## Repository Structure

Top-level source projects under `src/`:
  - `TcfOss.DatabaseManager.App/`—CLI entrypoint and commands (`Program.cs`,
    `CommandLineInterface.cs`). All CLI behavior is confined here.
  - `TcfOss.DatabaseManager.Core/`—core parsing, definitions, and database-agnostic logic.
  - `TcfOss.DatabaseManager.MySql/`, `TcfOss.DatabaseManager.MariaDb/`, and
    `TcfOss.DatabaseManager.MsSql/`—provider-specific overrides. MariaDB currently
    inherits MySQL's parser wholesale (no `MaParser`); it diverges only where
    needed in non-parser code.

## Changing Existing Behavior

1. Try to keep AST elements (`Statement`s and `Expression`s) provider-agnostic. Provider-specific
   fields on AST elements must use nullable types (nullable reference types such as `string?`, or nullable value types such as `int?`, as appropriate).
2. Provider-specific logic should be in the provider project's `Parser` implementation. See
   **Parser layering** below for the kitchen-sink / narrowing-override convention.
3. `DatabaseComms` implementations, and configuration parsing — the core AST types in `Core/Statements`
    and `Core/Expressions` should accommodate provider differences via nullable properties, not
    provider-specific subclasses.

### Parser layering

Core parsers are **kitchen-sink**: they parse every syntax recognised by any
supported dialect. Provider parsers **narrow** by overriding `virtual` hooks.
The goal is that any syntax shared by two or more dialects lives in Core exactly
once.

- `Core/Parsing/Parser.cs` is an orchestrator; parsing logic is split across
  sub-parsers (`DdlParser`, `DmlParser`, `SelectParser`, `TableParser`,
  `DataTypeParser`, `ExpressionParser`, `ComponentParser`, `ControlFlowParser`,
  `ValueParser`). Provider parsers (`MyParser`, `MsParser`) wire dialect-specific
  sub-parsers in their constructor and otherwise delegate.
- **Syntax shared by 2+ dialects (even with small differences)**: parse it in
  the Core sub-parser. Expose the dialect-varying piece as a `protected virtual
  ParseOptional*` hook returning a nullable component from
  `Core/DatabaseObjects/Components/` (e.g. `MsRoutineWithOptions?`). The Core
  default returns `null`; dialects that support the piece override to parse it;
  dialects that don't, do nothing.
- **Syntax supported by only one dialect, with irreconcilable shape**
  (e.g. T-SQL `IF` vs MySQL `IF ... THEN ... END IF`): expose a `virtual`
  dispatch method on the Core sub-parser whose default throws
  `NotSupportedException`. Each dialect overrides it entirely.
- **AST for dialect-specific pieces** lives in Core anyway (e.g.
  `Limit.MyCommaSeparated`, `MsTableHint`, `MyDataType.*`, `MsDataType.*`).
  Keep the dialect prefix on the type name; namespace stays in Core.
- Before adding a new override file or sub-parser, check whether the existing
  Core sub-parser already has a hook for the shape you need. Don't create a
  near-empty dialect file just to narrow one method — add the override to the
  existing dialect sub-parser, or skip the override if Core's default already
  does the right thing.

## Implementing New Behavior

1. Obey the rules in **Changing Existing Behavior** above for `Statements` and
   `Expressions` and their respective parsers.
2. Each provider's directory structure mirrors the structure of `TcfOss.DatabaseManager.Core/`
   (see **Core subfolders** below). Use the descriptions of what is in each folder as guidance.
3. To keep file names unambiguous, use an abbreviated (two-character) provider name as a prefix in
   dialect-specific file/class names (e.g. `MyParser` for MySQL and `MaParser` for MariaDB; when we
   get to them, we'll use `Pg` and `Ms` as the prefix for PostgreSQL and Microsoft SQL Server, respectively).

Project-wide build settings and analyzers live in `Directory.Build.props` and `Directory.Build.targets` at repository root—do not override without asking.

Any changes require corresponding updates to unit tests. Where possible, new integration
tests are also ideal (these are complicated to manage; consult the user before beginning).

## Core subfolders (`src/TcfOss.DatabaseManager.Core/`)

- `Lexing/`: Lexing logic and token definitions. All lexing behavior (converting text into tokens) is confined here.
- `Parsing/`: Parsing logic (converting tokens into an AST). All parsing behavior is confined here—follow existing token/AST styles when adding syntax or analyzers.
    - The main parser is in `Parser.cs`. To keep files manageable, parsing logic is split into
      multiple files (e.g., `SelectParser.cs`, `TableParser.cs`), all operating on the same token
      stream.
    - The token stream is maintained by a `ParserState` that is passed to all parsing methods,
      allowing look-ahead and backtracking without losing position.
- `Statements/`: Record classes representing parsed statements; each implements the `Statement`
  abstract class.
    - Dialect-specific properties must be nullable; dialect-specific parsers decide whether to populate them.
    - Non-primitive statement properties belong in one of:
        - `Attributes/`: Simple attributes that modify statement behavior — no more complex than
          an enum; use the `StringEnum` attribute to define allowed values (`StringEnumAttribute`
          triggers a source generator that creates an "enum with behavior").
        - `Components/`: More complex statement parts (e.g. `SimpleSelectItem`, `AlterTableOperation`).
        - `LabelAttributes/`: Optional keywords that do not affect behavior (e.g. `KEY` vs
          `INDEX` in a `CREATE INDEX` statement).
- `Expressions/`: Record classes representing parsed expressions; each implements the `Expression` abstract class.
- `DatabaseObjects/`: Definitions of database objects (e.g., `Table`, `StoredProcedure`) that
   specify the structure of the database.
     - `Components/`: Pieces that make up database objects (e.g., `Column`, `Index`,
       `RoutineParameter`).
     - `Attributes/`: Simple attributes that modify database object behavior (e.g., `ColumnOption`,
       `RoutineParameterDirection`).
- `DefinitionBuilding/`: Logic for constructing a database definition from parsed statements and
   other sources. It's primary purpose is to take objects defined in `Statements/` and `Expressions/` and build them into the more complete and structured objects in `DatabaseObjects/`, which are
   used for comparison and SQL generation.
- `DefinitionMapping/`: Logic for comparing two database definitions and generating statements to
   convert one into the other.
- `DatabaseComms/`: Database communication code for reading `INFORMATION_SCHEMA` or other database
   metadata.
- `Configuration/`: Core configuration models. Configuration is read and validated here and in
   provider `Configuration/` folders.
    1. `Configuration/Parsing/` is the target for YamlDotNet deserialization.
    2. If a configuration model can be used by the rest of the application as-is, place it in
       `Configuration/`.
    3. If further processing is needed (e.g., validation, transformation), place the raw deserialized
       model in the core `Configuration/Parsing/` folder and process it into a core configuration
       model in `Configuration/`.
- `BuiltIn/`: SQL keywords (`Keyword.cs`), data types (`DataType.cs`), function name provider
  interface, and other built-in SQL component representations.
- `Common/`: Shared value and identifier types (`Value`, `Identifier`, `ObjectName`,
  `CatalogIdentifier`, `SchemaIdentifier`, `ObjectIdentifier`, `ColumnIdentifier`, etc.).
- `Errors/`: Exception types for all application-specific errors. The exact wording of errors
  is set via the resource file in `Resources/ErrorMessages.resx`. If new errors are needed, consult
  the user before adding.

## Build and Test

- Build solution: `dotnet build DatabaseManager.slnx`
- Build single project: `dotnet build src/TcfOss.DatabaseManager.App/TcfOss.DatabaseManager.App.csproj`
- Run all unit tests: `dotnet test solutions/UnitTests.slnx`
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

Related classes that all implement the same base type, but are all relatively simple, are often grouped together following this format:

```csharp
public abstract record class BaseType
{
    // Common properties/methods for all variants of BaseType

    public record VariantA : BaseType
    {
        // Properties/methods specific to VariantA
    }

    public record VariantB : BaseType
    {
        // Properties/methods specific to VariantB
    }

    // Additional variants...
}
```

See `Common/Value.cs` for an example.

## Working Guidelines

- **Providers**: Changes that affect SQL generation or communication must be validated against both `src/TcfOss.DatabaseManager.MySql` and `src/TcfOss.DatabaseManager.MariaDb`.
- **Security**: Changes to `DatabaseComms` or any code that builds or executes SQL must be reviewed for injection risks—match existing escaping/parameterization patterns.
- **NuGet**: The repository-level `nuget.config` controls feeds—do not hardcode external package sources.
- **Human review**: Ask before making large refactors of `TcfOss.DatabaseManager.Core`, changes to CLI behavior in `Program.cs`/`CommandLineInterface.cs`, or changes to release/deployment scripts under `tools/`.
- **git**: A human will execute all `git add` and `git commit` commands. Agents may suggest commits and commit messages but must not execute them directly.
- **Package-local guidance**: This file is the workspace-wide default. For package-local instructions, add an `AGENTS.md` next to the package (e.g., `src/TcfOss.DatabaseManager.Core/AGENTS.md`).
