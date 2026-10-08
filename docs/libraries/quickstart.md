---
description: Parse SQL offline with the DatabaseManager Core LibWrapper and scoped dependency injection.
---

# Quick Start

Install `TcfOss.DatabaseManager.Core.LibWrapper` in a .NET 10 console application.
This complete example uses an explicit generic configuration and needs no
configuration file or database connection:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.LibWrapper;
using TcfOss.DatabaseManager.Core.Parsing;

var workingDirectory = Directory.GetCurrentDirectory();
var rawConfig = new GenericStartup().GetDefaultConfig(workingDirectory);
var result = new GenericStartApp().Start(
    rawConfig: rawConfig,
    workingDirectory: workingDirectory);

using var scope = result.Services.CreateScope();
var parser = scope.ServiceProvider.GetRequiredService<IParseText>();
var statements = parser.ParseText(
    "CREATE TABLE widgets (id INT);",
    filename: "widgets.sql");

Console.WriteLine($"Parsed {statements.Count} statement(s).");
```

`IParseText.ParseText` returns a `SqlValueList<Statement>` containing the AST, not
a database definition. `Statement` types live in
`TcfOss.DatabaseManager.Core.Statements`. The optional filename labels the input
for diagnostic purposes only.


## Startup Configuration

`Start` accepts these optional arguments:

| Argument | Meaning |
| --- | --- |
| `configFilePath` | Explicit path to a YAML configuration file. |
| `rawConfig` | A `Core.Configuration.Parsing.Config` supplied by your application instead of loading YAML. |
| `workingDirectory` | Starting point for configuration discovery; defaults to the current directory. |
| `relaxed` | Select relaxed configuration validation where the provider supports it. It does not enable unsupported syntax. |
| `options` | Host and service-registration hooks; see [customization](customization.md). |

Without `rawConfig`, startup searches for `database-manager.yaml` from the
working directory upwards, or uses the explicit file path. If no configuration
is found, provider defaults are used. Keep the selected wrapper and configured
`Dialect` consistent; a mismatch is logged and the selected wrapper's services
are used.

For a project using a configuration file:

```csharp
using TcfOss.DatabaseManager.MySql.LibWrapper;

var result = new MySqlStartApp().Start(
    configFilePath: "/path/to/project/database-manager.yaml",
    workingDirectory: "/path/to/project");
Console.WriteLine(result.Config.ProjectDirectory);
```

This second example requires the MySQL wrapper, an existing project directory,
and the [provider configuration](../app/configuration.md). Unlike generic
startup, MySQL/MariaDB startup can contact the server to discover defaults.


## Startup Results

| Property | Meaning |
| --- | --- |
| `Config` | Processed configuration, typed for the selected provider. |
| `Services` | The standard `IServiceProvider`; create scopes for scoped services. |
| `ServiceProvider` | The provider's convenience-access object. Prefer scoped `Services` access in application code. |
| `UsingDefaultConfig` | `true` when startup used fallback configuration rather than a file or supplied raw configuration. |

Scopes dispose services resolved within them. The result itself is not
`IDisposable` and does not expose the host for shutdown. Applications needing
explicit host ownership should use [manual hosting](customization.md#manual-hosting-without-libwrapper).
Avoid repeated wrapper startup as a replacement for service scopes.
