# TcfOss.DatabaseManager LibWrappers

The `*.LibWrapper` projects/packages are convenience wrappers for the primary
TcfOss.DatabaseManager class libraries. They are intended for consumers who want a
simple startup path without manually wiring the dependency graph for logging,
hosting, and provider-specific services.

See the [.NET library guide](https://tcfoss.github.io/database-manager/libraries/),
[provider workflows](https://tcfoss.github.io/database-manager/libraries/providers/), and
[source repository](https://github.com/tcfoss/database-manager).

## Packages

- `TcfOss.DatabaseManager.Core.LibWrapper`
- `TcfOss.DatabaseManager.MySql.LibWrapper`
- `TcfOss.DatabaseManager.MariaDb.LibWrapper`
- `TcfOss.DatabaseManager.MsSql.LibWrapper`

## Basic usage

The wrapper packages resolve configuration, initialize logging, and build a
service provider. This offline example uses the Core wrapper; install
`TcfOss.DatabaseManager.Core.LibWrapper` in a .NET 10 application:

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
var statements = parser.ParseText("CREATE TABLE widgets (id INT);");
Console.WriteLine($"Parsed {statements.Count} statement(s).");
```

## Advanced usage

If you need to customize the host or service registration, pass a `StartupOptions`
instance with your own hooks:

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

using var scope = result.Services.CreateScope();
var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
Console.WriteLine(clock.GetUtcNow());
```

Use the same hooks with the provider wrappers and a matching configuration.
Startup results are not disposable hosts; use [manual hosting](https://tcfoss.github.io/database-manager/libraries/customization/#manual-hosting-without-libwrapper)
when your application must own host shutdown.
