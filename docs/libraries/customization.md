---
description: Customize DatabaseManager startup or register its services in a caller-owned .NET host.
---

# Customization and Hosting

For direct construction without a host or DI container, see
[Without dependency injection](without-di.md).

## Startup Hooks

`StartupOptions` lives in `TcfOss.DatabaseManager.Core.LibWrapper`, including when
you use a provider wrapper. Hooks run after the library configures its services
and before the host is built. `ConfigureBuilder` runs first, then
`ConfigureServices`.

This complete Core wrapper example adds an application service without placeholder
types:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.LibWrapper;

var workingDirectory = Directory.GetCurrentDirectory();
var rawConfig = new GenericStartup().GetDefaultConfig(workingDirectory);
var result = new GenericStartApp().Start(
    rawConfig: rawConfig,
    workingDirectory: workingDirectory,
    options: new StartupOptions
    {
        ConfigureBuilder = builder => builder.Environment.ApplicationName = "MyApplication",
        ConfigureServices = services => services.AddSingleton(TimeProvider.System)
    });

var clock = result.Services.GetRequiredService<TimeProvider>();
Console.WriteLine(clock.GetUtcNow());
```

Use the same hooks with `MySqlStartApp`, `MariaDbStartApp`, or `SqlServerStartApp`.
Provider startup still needs its own valid configuration.


## Convenience Access

`AppServiceProvider` convenience accessors use thread-static state initialized by
startup. They are not per-result state and should not be carried across async
thread transitions. Prefer `result.Services.CreateScope()` and ordinary dependency
injection, especially in asynchronous applications.

Wrapper startup builds a host but does not expose it through the returned result
for explicit shutdown. Use the following approach when your application needs to
own and dispose the host.


## Manual Hosting Without LibWrapper

Install `TcfOss.DatabaseManager.Core` and `Microsoft.Extensions.Hosting` for this
standalone example. It registers generic services in a host owned by the caller,
without using a wrapper or static service-provider shortcuts:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Parsing;

var workingDirectory = Directory.GetCurrentDirectory();
var startup = new GenericStartup();
var rawConfig = startup.GetDefaultConfig(workingDirectory);
var config = startup.BuildConfiguration(
    workingDirectory, rawConfig, new EnvironmentVariableReader(),
    relaxed: false, logger: NullLogger.Instance);

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = workingDirectory,
    DisableDefaults = true
});
builder.Services.AddLogging();
builder.RegisterCommonServices(config);
builder.Services.RegisterGenericServices(config);

using var host = builder.Build();
var parser = host.Services.GetRequiredService<IParseText>();
var statements = parser.ParseText("CREATE TABLE widgets (id INT);");
Console.WriteLine($"Parsed {statements.Count} statement(s).");
```

`RegisterCommonServices` registers shared configuration and filesystem/parser
dependencies. The dialect registration supplies its lexer, parser, formatting,
and other supported services. Both registrations are required for this path.

For a caller-owned provider host, use its processed configuration and matching
registration instead of `RegisterGenericServices`:

| Package | Startup/configuration builder | Service registration | Extension namespace |
| --- | --- | --- | --- |
| `MySql` | `MyStartup` / `MyConfig` | `RegisterMySqlServices` | `TcfOss.DatabaseManager.MySql.App` |
| `MariaDb` | `MaStartup` / `MyConfig` | `RegisterMariaDbServices` | `TcfOss.DatabaseManager.MariaDb.App` |
| `MsSql` | `MsStartup` / `MsConfig` | `RegisterMsSqlServices` | `TcfOss.DatabaseManager.MsSql.App` |

Build configuration using the selected startup's `BuildConfiguration` with a
valid raw configuration, environment-variable reader, and logger, then call
`RegisterCommonServices` and the corresponding dialect registration. Provider
configuration may require connection information and contact the server; do not
substitute a generic configuration object. See the [provider prerequisites](providers.md).

Choose one dialect registration per service container. Static shortcut access
also requires separate initialization; the manual example intentionally uses DI
instead.
