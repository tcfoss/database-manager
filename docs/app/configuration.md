---
description: Configure SQL dialects, schemas, credentials, formatting, and logging for DatabaseManager.
---

# The Configuration File

The execution of the application is mostly controlled by a configuration file
at the root of your database project named `database-manager.yaml`. If your
file has a non-standard name, or you are running the application from a
different directory, see the `--config` flag described in
[the basic usage page](./index.md#the-configuration-file).

## Minimal example

A minimal MySQL configuration looks like this. MariaDB uses the same shape
with `Dialect: MariaDb`:

```yaml
Dialect: MySql

Schemas:
  - SchemaName: my_schema
    RootPath: my_schema

Credentials:
  Hostname: localhost
  Username: my_user
  Password: ${ENV:DB_PASSWORD}
```

For SQL Server, specify the catalog explicitly:

```yaml
Dialect: MsSql
Catalog: my_catalog
Schemas:
  - SchemaName: dbo
    RootPath: dbo
Credentials:
  Hostname: localhost
  Username: my_user
  Password: ${ENV:DB_PASSWORD}
```

SQL Server definition loading and migration generation are not implemented.

## Common Settings and MySQL/MariaDB Shape

The block below shows the common settings and MySQL/MariaDB-specific options.

```yaml
ProjectDirectory: <path to project root, defaults to the directory of this file>
Catalog: <catalog name> # defaults to "def" in MySQL/MariaDB; required in SQL Server
Dialect: { MySql | MariaDb | MsSql | Generic } # required
Version: <e.g. 8.0.36>
QuoteStyle: { Ansi | Backticks | Brackets }

Schemas:                                  # required, at least one entry
  - SchemaName: <name of schema in RDBMS>
    RootPath: <path to schema definition>
    IncludeFilePatterns:
      - "<glob>"
    ExcludeFilePatterns:
      - "<glob>"
    ExcludeDatabaseObjectNames:
      - <object name>
    DeployScripts:
      - FilePath: <path>
        Type: { PreDeployment | PostDropConstraints | PreSetNotNull | PreAddConstraints | PostDeployment }
        UniqueId: <guid>
    RefactorFiles:
      - <path>

Credentials:
  Hostname: <host>                        # required
  Username: <user>                        # required
  Password: <password>
  Port: <port, defaults to 3306>
  SocketPath: <unix socket path>

Formatting: { ... }                       # see formatting reference
DifferFormatting: { ... }                 # see formatting reference

DefaultDefinerAccount: <account>
DefaultDefinerHost: <hostname>

MinPoolSize: <minimum pooled connections, defaults to 0>
MaxPoolSize: <maximum pooled connections, defaults to 100>

Logging:
  Target: { File | Console }
  LogDirectory: <directory>
  LogLevel: { Trace | Debug | Information | Warning | Error | Critical | None }
  DatabaseLogLevel: { Trace | Debug | Information | Warning | Error | Critical | None }
  EnableSensitiveDataLogging: <bool>
```

## Top-level settings

| Key                     | Required | Default                              |
| ----------------------- | :------: | ------------------------------------ |
| `Dialect`               |   yes    | —                                    |
| `Schemas`               |   yes    | — (must contain at least one entry)  |
| `Credentials`           |  yes\*   | —                                    |
| `Catalog`               | provider-specific | `def` for MySQL/MariaDB; required in a SQL Server file |
| `ProjectDirectory`      |    no    | directory containing the config file |
| `Version`               |    no    | provider-specific; see below         |
| `QuoteStyle`            |    no    | `Backticks` for MySQL/MariaDB; `Brackets` for SQL Server |
| `Formatting`            |    no    | see [Formatting](./formatting.md)    |
| `DifferFormatting`      |    no    | see [Formatting](./formatting.md)    |
| `DefaultDefinerAccount` |   no\*   | —                                    |
| `DefaultDefinerHost`    |    no    | —                                    |
| `Logging`               |    no    | console at `Information` level       |

\* `Credentials` is required for any command that connects to the RDBMS. See
the per-command tables in [the basic usage page](./index.md).

### `Catalog`

A catalog is the top-level organizational element of the RDBMS—see the
[glossary](./index.md#definitions-of-terms). For MySQL and MariaDB it is
always `def`, at least for now. (An upcoming MariaDB version is expected to
support multiple catalogs as part of multi-tenancy work; how that will
interact with this project is not yet clear.)

What Microsoft SQL Server calls a "database" is what this application calls
a catalog. Catalog is required in SQL Server configuration files.

### `Dialect`

One of `MySql`, `MariaDb`, `MsSql`, or `Generic`.

`MsSql` selects Microsoft SQL Server syntax. Its current capabilities are
parsing and formatting, not definition comparison or migration generation.

The `MySql` and `MariaDb` dialects are very similar but **not interchangeable**.
Picking the wrong one will, in the best case, produce subtly wrong output, and
in the worst case crash the application when it tries to connect (the two
servers store character-set and collation information differently). Other
differences include:

- Different default character sets and collations.
- Different default display widths for integer types (deprecated by MySQL and
  therefore left null there, but still used by MariaDB).
- Slightly different support for routine parameter specification (e.g. MariaDB
  allows `OUT` parameters in stored functions; MySQL does not).
- Different processes for normalizing `VIEW` definitions.

The `Generic` dialect is intentionally limited: it supports some basic parsing
of SQL files to JSON, but does **not** support connecting to an RDBMS or any
form of definition analysis. It can format parsed statements without a definition.

### `Version`

The RDBMS version, as a dotted version string (for example `8.0.36`).

This is consulted **only when DatabaseManager cannot connect to the RDBMS**.
In that fallback case, `Version` is combined with `Dialect` to pick built-in
defaults for engine, character set, and collation. When a connection is
available, the live server is queried instead, and `Version` is ignored.

### `QuoteStyle`

Determines how identifiers are quoted in **generated** SQL scripts. It does
not change how input files are parsed. Options:

- `Backticks` — `` `name` `` (the default for MySQL/MariaDB)
- `Ansi` — `"name"`
- `Brackets` — `[name]` (the default for SQL Server)

MySQL and MariaDB have `sql_mode` flags that allow them to accept ANSI quotes,
while SQL Server natively uses brackets.

### `ProjectDirectory`

The root of your database project. Defaults to the directory containing the
configuration file, which is almost always what you want.

`ProjectDirectory` is the base for resolving each schema's `RootPath`. See
[Path resolution](#path-resolution) below.

### `DefaultDefinerAccount` and `DefaultDefinerHost`

For MySQL and MariaDB, default values to use for the definer in routines (procedures,
functions, triggers, views, and events) which do not explicitly specify a definer. If
the default definer should be a role, it should be specified in the `DefaultDefinerAccount`
property.

`DefaultDefinerHost` **cannot** be specified unless `DefaultDefinerAccount` is.

If a default definer is not specified, then a `DEFINER` clause **must** be specified
in **every** routine definition. Definers specified in a routine definition take
precedence over those specified in the project configuration.

## Schemas

The `Schemas` block lists the schemas managed by the database project. It is
**required** and must contain at least one entry. In MySQL/MariaDB, "schema"
is synonymous with "database".

### Required keys

| Key          | Description                                                                                       |
| ------------ | ------------------------------------------------------------------------------------------------- |
| `SchemaName` | Name of the schema as defined in the RDBMS.                                                       |
| `RootPath`   | Path to the directory containing the files that define this schema's tables, views, routines, etc. |

### Optional keys

#### `IncludeFilePatterns`

A list of glob patterns (using `.gitignore`-style syntax) that determine which
files under `RootPath` are parsed when building the database definition. If
omitted, the default is `**/*.sql`.

Quote glob entries that begin with `*`—YAML treats a leading `*` as an
alias reference and will reject the document otherwise:

```yaml
IncludeFilePatterns:
  - "**/tables_to_include/**"
```

#### `ExcludeFilePatterns`

A list of glob patterns that should **not** be parsed. The same quoting note
applies:

```yaml
ExcludeFilePatterns:
  - "**/pattern-to-exclude*.sql"
```

#### `ExcludeDatabaseObjectNames`

A list of objects (tables, triggers, stored procedures, etc.) that may exist
in the live RDBMS but should be ignored when constructing the database
definition that represents it.

For example, if a table `my_table` exists in the RDBMS but is not in your
definition files, `compute-changes` would by default emit a `DROP TABLE` for
it. Adding `my_table` to `ExcludeDatabaseObjectNames` tells the application
to leave it alone.

#### `DeployScripts`

A list of scripts to splice into the output of `compute-changes` at various
stages. See [Deploy scripts](./deploy-scripts.md) for full details.

#### `RefactorFiles`

A list of paths to YAML files defining refactors. In this project, "refactor"
means **renames** of tables and columns. See [Refactors](./refactors.md) for
the file format.

### Path resolution

Paths inside the configuration file are resolved as follows:

1. A relative `RootPath` on a schema mapping is resolved against
   `ProjectDirectory`.
2. Relative paths nested under a schema mapping (`DeployScripts.FilePath`,
   `RefactorFiles` entries, and the targets of include/exclude patterns)
   are resolved against that schema's `RootPath`.
3. `ProjectDirectory` defaults to the directory containing the configuration
   file, so in practice "relative to `ProjectDirectory`" and "relative to the
   config file" usually mean the same thing.

## Credentials

The `Credentials` section tells DatabaseManager how to connect to your RDBMS.
The following table applies to MySQL/MariaDB:

| Key          | Required | Default |
| ------------ | :------: | ------- |
| `Hostname`   |   yes    | —       |
| `Username`   |   yes    | —       |
| `Password`   |    no    | —       |
| `Port`       |    no    | `3306`  |
| `SocketPath` |    no    | —       |

Values are forwarded to the underlying database driver (MySqlConnector) when
building the connection string. If `SocketPath` is set, a Unix-socket
connection is used; otherwise a TCP connection to `Hostname:Port` is used.
Whether `Password` is required depends on your server's authentication
configuration.

### SQL Server Credentials

SQL Server uses `Hostname`, optional `Username` and `Password`, and a `Port`
that defaults to `1433`. Omitting `Username` selects integrated authentication;
its availability depends on your platform and SQL Server setup.

| Key | Default | Purpose |
| --- | --- | --- |
| `Instance` | none | Named SQL Server instance; takes precedence over `Port`. |
| `Encrypt` | `true` | Encrypt the connection. |
| `TrustServerCertificate` | `false` | Skip certificate validation when encryption is enabled. |
| `ConnectionTimeout` | `30` | Connection timeout in seconds. |


### Environment-variable interpolation

String values under `Credentials` may reference an environment variable with the
syntax `${ENV:VARIABLE_NAME}`. The literal `ENV` is **case-sensitive**;
whether `VARIABLE_NAME` is case-sensitive depends on your operating system.

```yaml
Credentials:
  Hostname: ${ENV:DB_HOST}
  Username: ${ENV:DB_USER}
  Password: ${ENV:DB_PASSWORD}
  Port: ${ENV:DB_PORT}
```

This substitution applies only inside `Credentials`.

## Formatting and DifferFormatting

The `Formatting` block controls how the application emits SQL when formatting
or rewriting files. The `DifferFormatting` block controls SQL emitted by the
diffing pipeline (`compute-changes`). Both blocks are optional.

See [Formatting](./formatting.md) for the full list of keys.

## Logging

The `Logging` section is optional and controls diagnostic output.

| Key                          | Type                                                                | Default                              |
| ---------------------------- | ------------------------------------------------------------------- | ------------------------------------ |
| `Target`                     | `Console` \| `File`                                                 | `Console`                            |
| `LogDirectory`               | path                                                                | the system temp directory            |
| `LogLevel`                   | `Trace` \| `Debug` \| `Information` \| `Warning` \| `Error` \| `Critical` \| `None` | `Information` |
| `DatabaseLogLevel`           | same as `LogLevel`                                                  | `Warning`                            |
| `EnableSensitiveDataLogging` | bool                                                                | `false`                              |

`LogLevel` controls the application's own log output. `DatabaseLogLevel`
controls how verbosely the underlying database driver and ORM are logged.

When `Target` is `File`, each run of the application writes a new log file
named `database-manager_YYYYMMDD_HHMMSS_ffffff.json` (microsecond-precision
timestamp) inside `LogDirectory`.
