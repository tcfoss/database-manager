---
description: Build, test, and contribute to DatabaseManager.
---

# Contributing

## Getting started

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
The repository's `global.json` selects the supported SDK feature bands.

From the repository root:

```sh
dotnet build DatabaseManager.slnx
dotnet test --solution solutions/UnitTests.slnx
dotnet format --severity info --verify-no-changes
```

See [integration tests](integration-tests.md) for Docker-backed test infrastructure,
[releases](releases.md) for the release process, and the
[development tools](dbman-dev/index.md) for Entity Framework code generation.
If you have Docker installed, you can also run the integration test suite:

```sh
dotnet test --solution solutions/IntegrationTest.slnx
```

Package consumers should use the [library guide](../libraries/index.md) rather
than these contributor instructions.

## Core folder guide

The shared library lives in `src/TcfOss.DatabaseManager.Core/`. Provider projects
use corresponding folders where they need dialect-specific implementations.

### Lexing and parsing

- `Lexing/` contains the lexer and token definitions: conversion from SQL text
	into tokens.
- `Parsing/` converts tokens into the statement and expression AST. `Parser.cs`
	orchestrates sub-parsers such as `DdlParser`, `SelectParser`, and `TableParser`.
	They share a `ParserState` token stream for look-ahead and backtracking.

### Parsed statements and expressions

- `Statements/` contains records derived from `Statement` that preserve parsed
	SQL syntax, including optional dialect-specific clauses.
- `Statements/Attributes/` contains simple enum-like modifiers, commonly defined
	with `StringEnumAttribute`.
- `Statements/Components/` contains structured statement parts, such as
	`SimpleSelectItem`, `AlterTableOperation`, and `StatementFunctionReturn`.
- `Statements/LabelAttributes/` contains optional spelling choices that do not
	change behavior, such as `KEY` versus `INDEX`.
- `Expressions/` contains records derived from `Expression`.

Dialect-specific components can live in Core so shared ASTs can represent them.
Optional dialect-specific additions on shared nodes are nullable; required
fields inside a component or variant remain non-nullable.

### Database definitions

- `DatabaseObjects/` contains resolved objects such as tables, views, and stored
	routines, used for comparison and SQL generation.
- `DatabaseObjects/Components/` contains their structured parts, such as columns,
	indexes, routine parameters, and resolved function returns.
- `DatabaseObjects/Attributes/` contains simple object modifiers, such as
	`ColumnOption` and `RoutineParameterDirection`, including attributes shared
	with parsed statements.
- `DefinitionBuilding/` constructs resolved definitions from statements,
	expressions, and other sources, including name and type resolution.
- `DefinitionMapping/` compares definitions and generates statements that
	transform one definition into another.
- `DatabaseComms/` reads database metadata, for example from `INFORMATION_SCHEMA`
	or provider system catalogs.

### Configuration and shared infrastructure

- `Configuration/` contains configuration models, processing, and validation.
	Models that the rest of the application can use directly belong here.
- `Configuration/Parsing/` contains raw models targeted by YamlDotNet
	deserialization when further validation or transformation is needed.
- `BuiltIn/` contains SQL keywords, data types, and built-in function-name
	abstractions.
- `Common/` contains shared value and identifier types, including `Value`,
	`Identifier`, `ObjectName`, and catalog/schema/object/column identifiers.
- `Errors/` contains application-specific exception types; resource-backed error
	messages live in `Resources/ErrorMessages.resx`.


## Feedback and Pull Requests

Bugs and feature requests can be logged on GitHub. However, the "source of truth" for this project is [IssueTracker](https://issues.tcflanagan.net/database-manager). You can log in via your GitHub account and you should be able to create work items.

If you would like to actively contribute to implementing features or fixing bugs, reach out and I can upgrade your permissions on IssueTracker.

See the [repository contribution checklist](https://github.com/tcfoss/database-manager#reporting-issues-and-contributing)
before opening a pull request.


## Documentation

Create a Python virtual environment and install the pinned documentation tools:

```sh
python3 -m venv .venv
```

On Linux/macOS, activate it with `source .venv/bin/activate`; in Windows
PowerShell, use `.venv\Scripts\Activate.ps1`. Then run:

```sh
python -m pip install -r requirements-docs.txt
mkdocs build --strict
mkdocs serve
```

The preview runs at `http://127.0.0.1:8000/database-manager/`.

With Node.js 24 installed, run the release-policy regression tests locally:

```sh
node --test .github/scripts/docs-release.test.cjs
```
