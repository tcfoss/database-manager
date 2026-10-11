---
description: Choose and install DatabaseManager .NET packages for SQL parsing, formatting, and MySQL/MariaDB definition comparison.
---

# .NET Libraries

DatabaseManager packages target **.NET 10**. Use a provider's `Hosting` package
for configuration loading, logging, and dependency-injection setup. Use the
same package for service registration in a caller-owned host. Use the underlying
library when constructing the needed objects manually.
Provider and hosting packages bring their Core dependencies transitively.

## Package Selection

| Package | Purpose |
| --- | --- |
| [TcfOss.DatabaseManager.Core](https://www.nuget.org/packages/TcfOss.DatabaseManager.Core) | Shared SQL AST, parsing, configuration, and definition-processing abstractions. Generic parsing and formatting without a server. |
| [TcfOss.DatabaseManager.MySql](https://www.nuget.org/packages/TcfOss.DatabaseManager.MySql) | MySQL parsing, formatting, definition loading, and migration-script generation. |
| [TcfOss.DatabaseManager.MariaDb](https://www.nuget.org/packages/TcfOss.DatabaseManager.MariaDb) | MariaDB services, sharing MySQL syntax parsing while applying MariaDB defaults and server metadata behavior. |
| [TcfOss.DatabaseManager.MsSql](https://www.nuget.org/packages/TcfOss.DatabaseManager.MsSql) | SQL Server parsing and formatting. Definition loading, schema download, and migration generation are not implemented. |
| [TcfOss.DatabaseManager.Core.Hosting](https://www.nuget.org/packages/TcfOss.DatabaseManager.Core.Hosting) | Generic startup, shared service registration, and logging infrastructure. |
| [TcfOss.DatabaseManager.MySql.Hosting](https://www.nuget.org/packages/TcfOss.DatabaseManager.MySql.Hosting) | MySQL startup through `MySqlStartApp`. |
| [TcfOss.DatabaseManager.MariaDb.Hosting](https://www.nuget.org/packages/TcfOss.DatabaseManager.MariaDb.Hosting) | MariaDB startup through `MariaDbStartApp`. |
| [TcfOss.DatabaseManager.MsSql.Hosting](https://www.nuget.org/packages/TcfOss.DatabaseManager.MsSql.Hosting) | SQL Server startup through `SqlServerStartApp`. |

## Installation

For the offline [quick start](quickstart.md):

```sh
dotnet add package TcfOss.DatabaseManager.Core.Hosting
```

For a provider, choose the matching hosting package instead:

```sh
dotnet add package TcfOss.DatabaseManager.MySql.Hosting
```

Substitute `MariaDb` or `MsSql` for the other providers. The hosting packages also
provide registration APIs for caller-owned hosts. See [customization](customization.md).

## Breaking Migration

The former `TcfOss.DatabaseManager.*.LibWrapper` packages and namespaces are now
`TcfOss.DatabaseManager.*.Hosting`. Replace the package references and imports.

Startup classes, service-registration extensions, and service-provider accessors
previously in the Core/provider `.App` namespaces now live in the matching
`.Hosting` packages and namespaces. Runtime command runners, formatters, and
environment-reading interfaces remain in the underlying libraries. The CLI uses
the same hosting integration as library consumers.

Configuration discovery and preparation are available independently in the base
packages: use `ConfigurationFile` and the corresponding `.Configuration` builder
(`GenericConfigurationBuilder`, `MyConfigurationBuilder`, `MaConfigurationBuilder`,
or `MsConfigurationBuilder`). Hosting startup delegates to these same APIs. See
[Without dependency injection](without-di.md#configuration-files).


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
