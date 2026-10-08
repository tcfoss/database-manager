---
description: Choose and install DatabaseManager .NET packages for SQL parsing, formatting, and MySQL/MariaDB definition comparison.
---

# .NET Libraries

DatabaseManager packages target **.NET 10**. Use a provider's `LibWrapper` package
for configuration loading, logging, and dependency-injection setup. Use the
underlying library when your application owns its host and service registration or when
you would prefer to `new`-up all needed objects manually.
Provider and wrapper packages bring their Core dependencies transitively.

## Package Selection

| Package | Purpose |
| --- | --- |
| [TcfOss.DatabaseManager.Core](https://www.nuget.org/packages/TcfOss.DatabaseManager.Core) | Shared SQL AST, parsing, configuration, and definition-processing abstractions. Generic parsing and formatting without a server. |
| [TcfOss.DatabaseManager.MySql](https://www.nuget.org/packages/TcfOss.DatabaseManager.MySql) | MySQL parsing, formatting, definition loading, and migration-script generation. |
| [TcfOss.DatabaseManager.MariaDb](https://www.nuget.org/packages/TcfOss.DatabaseManager.MariaDb) | MariaDB services, sharing MySQL syntax parsing while applying MariaDB defaults and server metadata behavior. |
| [TcfOss.DatabaseManager.MsSql](https://www.nuget.org/packages/TcfOss.DatabaseManager.MsSql) | SQL Server parsing and formatting. Definition loading, schema download, and migration generation are not implemented. |
| [TcfOss.DatabaseManager.Core.LibWrapper](https://www.nuget.org/packages/TcfOss.DatabaseManager.Core.LibWrapper) | Generic startup and shared wrapper configuration/logging infrastructure. |
| [TcfOss.DatabaseManager.MySql.LibWrapper](https://www.nuget.org/packages/TcfOss.DatabaseManager.MySql.LibWrapper) | MySQL startup through `MySqlStartApp`. |
| [TcfOss.DatabaseManager.MariaDb.LibWrapper](https://www.nuget.org/packages/TcfOss.DatabaseManager.MariaDb.LibWrapper) | MariaDB startup through `MariaDbStartApp`. |
| [TcfOss.DatabaseManager.MsSql.LibWrapper](https://www.nuget.org/packages/TcfOss.DatabaseManager.MsSql.LibWrapper) | SQL Server startup through `SqlServerStartApp`. |

## Installation

For the offline [quick start](quickstart.md):

```sh
dotnet add package TcfOss.DatabaseManager.Core.LibWrapper
```

For a provider, choose the matching wrapper instead:

```sh
dotnet add package TcfOss.DatabaseManager.MySql.LibWrapper
```

Substitute `MariaDb` or `MsSql` for the other providers. For manual hosting, install
the underlying package, such as `TcfOss.DatabaseManager.MySql`, and the
`Microsoft.Extensions.Hosting` package. See [customization](customization.md).

These are libraries, not the CLI distribution. See the [CLI guide](../app/index.md)
for application commands. Consult the package version on NuGet when selecting a
release. This site documents the latest stable release, not unreleased changes
on `master`; older packages may not expose every API documented here.

## Debugging Package Code

Published library packages have matching portable `.snupkg` symbols on NuGet.org.
Enable Source Link support in your debugger and add
`https://symbols.nuget.org/download/symbols` as a symbol source to step into
DatabaseManager code. Source Link retrieves tracked source from the release
commit; generated and other untracked source files are embedded in the PDB.

## Next Steps

- [Quick start](quickstart.md): parse SQL without an RDBMS.
- [Provider workflows](providers.md): load a MySQL/MariaDB definition and generate changes.
- [Customization and hosting](customization.md): register services and own your host.
- [Without dependency injection](without-di.md): construct parsers, formatters, and definition-processing objects directly.
- [Configuration](../app/configuration.md): the shared YAML configuration reference.
- [Source](https://github.com/tcfoss/database-manager/tree/master/src): implementation and public contracts.

The parser is oriented toward database-definition management, not every SQL
construct supported by every server. See the [definition limitations](../app/limitations.md).

!!! warning "Review generated SQL"
    Migration scripts can contain destructive changes. Review and test them, and
    back up data before execution. Generating a script does not execute it.