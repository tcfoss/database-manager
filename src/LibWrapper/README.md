# TcfOss.DatabaseManager LibWrappers

The `*.LibWrapper` projects/packages are convenience wrappers for the primary
TcfOss.DatabaseManager class libraries. They are intended for consumers who want a
simple startup path without manually wiring the dependency graph for logging,
hosting, and provider-specific services.

## Packages

- `TcfOss.DatabaseManager.Core.LibWrapper`
- `TcfOss.DatabaseManager.MySql.LibWrapper`
- `TcfOss.DatabaseManager.MariaDb.LibWrapper`
- `TcfOss.DatabaseManager.MsSql.LibWrapper`

## Basic usage

The wrapper packages expose a simple start entry point that resolves config,
initializes logging, and builds a service provider for you.

```csharp
using TcfOss.DatabaseManager.MySql.LibWrapper;

var app = new MySqlStartApp();
var result = app.Start(workingDirectory: "/path/to/project");

Console.WriteLine(result.Config.ProjectDirectory);
```

## Advanced usage

If you need to customize the host or service registration, pass a `StartupOptions`
instance with your own hooks:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.MsSql.LibWrapper;

var app = new SqlServerStartApp();
var result = app.Start(
    workingDirectory: "/path/to/project",
    options: new StartupOptions
    {
        ConfigureBuilder = builder =>
        {
            builder.Services.AddSingleton<ITestService, TestService>();
        }
    });

var service = result.Services.GetRequiredService<ITestService>();
```
