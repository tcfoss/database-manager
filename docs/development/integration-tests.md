# Integration Tests

Integration tests live under `test/` and are grouped into five projects, all included in `solutions/IntegrationTest.slnx`.

## Projects

| Project | Purpose |
|---|---|
| `TcfOss.DatabaseManager.Core.IntegrationTests` | Shared infrastructure reused by all other integration test projects |
| `TcfOss.DatabaseManager.MySql.IntegrationTests` | MySQL and MariaDB database fixtures and helpers |
| `TcfOss.DatabaseManager.MariaDb.IntegrationTests` | MariaDB-specific database fixtures |
| `TcfOss.DatabaseManager.MsSql.IntegrationTests` | SQL Server database fixtures and helpers |
| `TcfOss.DatabaseManager.App.IntegrationTests` | CLI end-to-end tests; depends on all of the above |


## Core integration test infrastructure

`TcfOss.DatabaseManager.Core.IntegrationTests` contains primitives shared across all providers.

### FakeDb

`FakeDatabaseTestContainer/` contains `FakeDbBuilder`, `FakeDbConfiguration`, and `FakeDbContainer`.
 `FakeDbContainer` implements `IDatabaseContainer` but performs no real network I/O—it is used for
 tests that exercise CLI behavior that does not require a live database connection
 (e.g. parse errors, file generation, no-connection error messages).

### FsProjectFixture

`FsProjectFixture` creates an isolated temporary directory at construction time and exposes it
as `RootDirectory`. Tests copy schema files into it and run the CLI against it.
The fixture disposes the directory when the test is done.


## Provider database fixtures

Each provider project owns version-pinned database fixtures. These are the objects that actually
manage the lifecycle of a Docker container.

### MySQL and MariaDB (`TcfOss.DatabaseManager.MySql.IntegrationTests`)

```
DatabaseFixtures/
    DbFixture<TBuilder, TContainer, TContext>   ← abstract; extends DbContainerFixture<,> (Testcontainers.Xunit)
    FakeDbFixture                               ← wraps FakeDbContainer; no container started
    MySqlFixture_[Version]                      ← sealed; one per pinned MySQL image tag
```

`DbFixture<,,>` extends `DbContainerFixture<TBuilder, TContainer>` from Testcontainers.Xunit,
which owns the container start/stop lifecycle and is itself an `IAsyncLifetime`. It adds:
- A `GetDbContextOptions()` method for constructing an EF Core `DbContext` against the container.
- A `GetLibrarySchemaConfig()` helper for building provider configuration.
- An `InitializeLibrarySchema()` helper that runs the library-schema initialisation script
  inside the container.

Each `MySqlFixture_[Version]` is sealed, calls `builder.WithStandardOptions("mysql:[tag]")`
in `Configure`, and specifies the dialect, port, and username.

MariaDB fixtures follow the same shape but live in
`TcfOss.DatabaseManager.MariaDb.IntegrationTests/DatabaseFixtures/` and use
the `MariaDbBuilder`/`MariaDbContainer` types from Testcontainers.

### SQL Server (`TcfOss.DatabaseManager.MsSql.IntegrationTests`)

```
DatabaseFixtures/
    DbFixture<TBuilder, TContainer, TContext>   ← abstract; extends DbContainerFixture<,> (Testcontainers.Xunit)
    FakeDbFixture                               ← wraps FakeDbContainer
    MsSqlFixture_[Version]                      ← sealed; one per pinned SQL Server image tag
```

The SQL Server `DbFixture<,,>` follows the same pattern as MySQL's.


## App integration tests (`TcfOss.DatabaseManager.App.IntegrationTests`)

These tests exercise the CLI end-to-end. They are organised into two test families—MySQL/MariaDB
and SQL Server—each with its own fixture and test class hierarchy.

### MySQL/MariaDB read-only hierarchy

The read-only tests verify CLI commands that do not mutate the database (parse-files,
download-schema for error cases, etc.).

#### Fixture hierarchy

```
CliReadOnlyFixture                                  ← non-generic abstract; IAsyncLifetime
│   Properties: IDatabaseContainer Container
│               FsProjectFixture InitialFsFixture
│
└── CliReadOnlyFixture<TBuilder, TContainer, TContext>   ← generic abstract
    │   Holds DbFixture<,,> (from MySql.IntegrationTests)
    │   InitializeAsync: starts container, runs InitializeLibrarySchema(),
    │                    copies Initial schema files into InitialFsFixture
    │
    └── MySqlFamily/CliReadOnlyFixture_NoDatabase        ← concrete; uses FakeDb (no container)
    └── MySql/CliReadOnlyFixture_MySql_[Version]         ← concrete; wraps MySqlFixture_[Version]
    └── MariaDb/CliReadOnlyFixture_MariaDb_[Version]     ← concrete; wraps MariaDbFixture_[Version]
```

#### Test class hierarchy

```
CliReadOnlyTests<TFixture>                           ← abstract; IClassFixture<TFixture>
│   where TFixture : CliReadOnlyFixture
│   ~1300 lines of [Fact] test methods
│   Virtual properties: Dialect, charset, collation, etc.
│
└── MySqlFamily/CliReadOnlyTests_NoDatabase          ← abstract; fixes TFixture = CliReadOnlyFixture_NoDatabase
│       Overrides: ConnectionAvailable = false, DownloadSchema
│
│   └── MySql/CliReadOnlyTests_MySql_NoDatabase      ← concrete; sets Dialect = MySql
│   └── MariaDb/CliReadOnlyTests_MariaDb_NoDatabase  ← concrete; sets Dialect = MariaDb
│
└── MySql/CliReadOnlyTests_MySql_[Version]           ← concrete; TFixture = CliReadOnlyFixture_MySql_[Version]
└── MariaDb/CliReadOnlyTests_MariaDb_[Version1]      ← concrete
└── MariaDb/CliReadOnlyTests_MariaDb_[Version2]      ← concrete
```

### MySQL/MariaDB read-write hierarchy

The read-write tests verify commands that modify the database (apply, diff, etc.). They use a
different base because each test needs a fresh container rather than a shared one.

```
CliReadWriteTests<TBuilder, TContainer, TContext>    ← abstract; extends ContainerTest<TBuilder, TContainer>
│   (ContainerTest from Testcontainers.Xunit — provides a per-test Container instance)
│
└── MySqlFamily/CliReadWriteTests_NoDatabase         ← abstract; fixes type params to FakeDb types
│
│   └── MySql/CliReadWriteTests_MySql_NoDatabase     ← concrete
│   └── MariaDb/CliReadWriteTests_MariaDb_NoDatabase ← concrete
│
└── MySql/CliReadWriteTests_MySql_[Version]          ← concrete
└── MariaDb/CliReadWriteTests_MariaDb_[Version1]     ← concrete
└── MariaDb/CliReadWriteTests_MariaDb_[Version2]     ← concrete
```

`CliReadWriteTests` inherits `ContainerTest<,>` directly—not `IClassFixture`—so the container
lifecycle is per-test, not per-class. This is intentional: read-write tests mutate the database,
so each test must start from a clean state.

### SQL Server read-only hierarchy

SQL Server currently has only read-only tests.

#### Fixture hierarchy

```
MsSql/CliReadOnlyFixture_MsSql                          ← non-generic abstract; IAsyncLifetime
│
└── MsSql/CliReadOnlyFixture_MsSql<TBuilder, TContainer>    ← generic abstract
    │   Holds DbFixture<,,SysContext> (from MsSql.IntegrationTests)
    │   InitializeAsync: starts container only (no schema init script)
    │
    └── MsSql/CliReadOnlyFixture_MsSql_FakeDb               ← concrete; uses FakeDb
    └── MsSql/CliReadOnlyFixture_MsSql_[Version1]            ← concrete; wraps MsSqlFixture_[Version1]
    └── MsSql/CliReadOnlyFixture_MsSql_[Version2]            ← concrete; wraps MsSqlFixture_[Version2]
```

#### Test class hierarchy

```
MsSql/CliReadOnlyTests_MsSql<TFixture>              ← abstract; IClassFixture<TFixture>
│   where TFixture : CliReadOnlyFixture_MsSql
│   [Fact] test methods for parse-files, parse errors, lex errors
│
└── MsSql/CliReadOnlyTests_MsSql_NoDatabase          ← concrete; TFixture = CliReadOnlyFixture_MsSql_FakeDb
└── MsSql/CliReadOnlyTests_MsSql_[Version1]          ← concrete
└── MsSql/CliReadOnlyTests_MsSql_[Version2]          ← concrete
```


## Adding a new database version

1. **Add a fixture** in the relevant provider's `DatabaseFixtures/` folder (e.g.
   `MySqlFixture_[NewVersion]` in `TcfOss.DatabaseManager.MySql.IntegrationTests`).
   Pin the Docker image tag in `Configure`.
2. **Add a CLI fixture** in `App.IntegrationTests` under the appropriate provider subfolder (e.g.
   `MySql/CliReadOnlyFixture_MySql_[NewVersion]`). Inherit the generic `CliReadOnlyFixture<,,>` or
   `CliReadOnlyFixture_MsSql<,>` and pass the new fixture to `DbFixture`.
3. **Add a test class** (e.g. `MySql/CliReadOnlyTests_MySql_[NewVersion]`) inheriting
   `CliReadOnlyTests<CliReadOnlyFixture_MySql_[NewVersion]>`. Override dialect-specific virtual
   properties as needed.
4. For read-write tests, add a class inheriting `CliReadWriteTests<,,>`, and
   implement `Configure` to pin the image tag.


## Shared namespace convention

The `MySqlFamily/` subdirectory of `App.IntegrationTests` (namespace `...MySqlFamily`) holds
infrastructure shared between MySQL and MariaDB that does not belong to either provider
specifically—currently the no-database fixture and abstract no-database test bases.
SQL Server shared infrastructure lives directly in `MsSql/`.
