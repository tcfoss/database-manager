---
description: Select DatabaseManager providers and load MySQL/MariaDB definitions or generate migration scripts.
---

# Provider Workflows


## Entry Points and Capabilities

| Wrapper | Entry point | Processed configuration | Current capabilities |
| --- | --- | --- | --- |
| `Core.LibWrapper` | `GenericStartApp` | `ConfigGeneric` | Generic parsing and formatting. |
| `MySql.LibWrapper` | `MySqlStartApp` | `MyConfig` | Parsing, formatting, definition loading, schema download, and migration-script generation. |
| `MariaDb.LibWrapper` | `MariaDbStartApp` | `MyConfig` | MariaDB versions of the MySQL-family workflows. |
| `MsSql.LibWrapper` | `SqlServerStartApp` | `MsConfig` | SQL Server parsing and formatting only. |

Each entry-point class is in its package's namespace, for example
`TcfOss.DatabaseManager.MariaDb.LibWrapper`. SQL Server does not currently register
filesystem/database definition loaders, schema download, or `IComputeChanges`.

MySQL and MariaDB share parsing code and definition types, but their server
defaults and metadata processing differ. Select the wrapper matching the server.


## Load a Filesystem Definition

Install `TcfOss.DatabaseManager.MySql.LibWrapper` and prepare an existing project
directory with `database-manager.yaml`, schema mappings, and SQL definition files
as described in the [configuration reference](../app/configuration.md).
Startup may connect to the configured server to retrieve metadata/defaults.

```csharp
using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.LibWrapper;

var result = new MySqlStartApp().Start(
    configFilePath: "/path/to/project/database-manager.yaml",
    workingDirectory: "/path/to/project");

using var scope = result.Services.CreateScope();
var loader = scope.ServiceProvider.GetRequiredService<ILoadFsDefinition<MyDefinition>>();
var definition = loader.LoadDefinition(relaxed: false);
Console.WriteLine($"Loaded {definition.Tables.Count} table(s).");
```

Unlike the parser's statement AST, `MyDefinition` groups tables, views, procedures,
functions, triggers, and events into a complete database definition. Loading applies
definition-building checks; see [limitations](../app/limitations.md).

For MariaDB, use `MariaDbStartApp` and its namespace. The loader's generic type
remains `MyDefinition`, from the MySQL package, because MariaDB reuses that model.


## Generate a Migration Script

The following example requires the same project assets and a live MySQL server.
The configured account needs access to inspect the relevant server metadata.
Choose an output path that your application can write:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MySql.LibWrapper;

var result = new MySqlStartApp().Start(
    configFilePath: "/path/to/project/database-manager.yaml",
    workingDirectory: "/path/to/project");

using var scope = result.Services.CreateScope();
var changes = scope.ServiceProvider.GetRequiredService<IComputeChanges>();
await changes.ExecuteAsync(
    outputFilePath: "/path/to/project/changes.sql",
    fileExistsAction: FileExistsAction.Error,
    cancellationToken: CancellationToken.None);
```

`ExecuteAsync` loads the configured filesystem and live-server definitions and
writes SQL to reconcile the live server with the files. The change computer does not treat the
filesystem and live-server definitions symmetrically and is not recommended for comparing two
arbitrary caller-supplied ASTs.

The output may include `DROP`
statements, configured [deploy scripts](../app/deploy-scripts.md), and
[refactors](../app/refactors.md).

`FileExistsAction.Error` protects an existing output file. Other supported actions
include `Rename`, `Overwrite`, and `Skip`.

!!! warning
    Review and test the script before applying it, and back up affected data.
    This API generates the script; it does not apply the generated changes to the server.


## SQL Server Parsing

Install `TcfOss.DatabaseManager.MsSql.LibWrapper`, start `SqlServerStartApp`, and
resolve `IParseText` in a scope as in the [quick start](quickstart.md). Use
`Dialect: MsSql` and an explicit catalog in a supplied configuration file.
The default SQL Server startup configuration is also available from
`new MsStartup().GetDefaultConfig(workingDirectory)` in
`TcfOss.DatabaseManager.MsSql.App`.

Since SQL Server support is in its infancy and does not yet support any actual communication with a
live server, Startup currently uses a configured/default server version rather than
discovering one over a connection. Definition-loading and migration APIs are not yet implemented.
