---
description: Construct DatabaseManager parsers, formatters, definition loaders, and comparison objects directly without a DI container or host.
---

# Without Dependency Injection

The underlying libraries can be used without a host, service provider, or
`LibWrapper`. Construct the objects directly and supply their dependencies.
Install `TcfOss.DatabaseManager.Core` for the parsing and formatting examples,
or `TcfOss.DatabaseManager.MySql` for the MySQL definition example.


## Parse SQL

For generic parsing, the complete object graph is just a lexer, parser, and `TextParser`:

```csharp
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

var lexer = new GenericLexer();
var parser = new Parser();
var textParser = new TextParser(lexer, parser);
var statements = textParser.ParseText(
    "CREATE TABLE widgets (id INT);",
    filename: "widgets.sql");

Console.WriteLine($"Parsed {statements.Count} statement(s).");
```

No configuration or database connection is needed. Choose a matching lexer/parser
pair for dialect-specific syntax:

| Syntax | Lexer | Parser | Namespaces |
| --- | --- | --- | --- |
| Generic | `GenericLexer` | `Parser` | `TcfOss.DatabaseManager.Core.Lexing` and `.Parsing` |
| MySQL/MariaDB | `MyLexer` | `MyParser` | `TcfOss.DatabaseManager.MySql.Lexing` and `.Parsing` |
| SQL Server | `MsLexer` | `MsParser` | `TcfOss.DatabaseManager.MsSql.Lexing` and `.Parsing` |

For example, MySQL and MariaDB syntax both use
`new TextParser(new MyLexer(), new MyParser())`. The other provider workflows
still have distinct configuration and metadata behavior.


## Format SQL in Memory

Formatting also needs configuration and a built-in function-name provider.
`BuildConfiguration` processes configuration without creating a host or DI
container. This complete generic example uses defaults and does not contact a
server:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

var workingDirectory = Directory.GetCurrentDirectory();
var startup = new GenericStartup();
var rawConfig = startup.GetDefaultConfig(workingDirectory);
var config = startup.BuildConfiguration(
    workingDirectory, rawConfig, new EnvironmentVariableReader(),
    relaxed: false, logger: NullLogger.Instance);

var textParser = new TextParser(new GenericLexer(), new Parser());
var functionNames = new FunctionNameProvider();
using var formatter = new Formatter(config, textParser, functionNames);
var formatted = formatter.GetFormatted("create table widgets(id int);");

Console.WriteLine(formatted);
```

`GetFormatted` returns the formatted SQL and clears the formatter's output
buffer. This example reads and writes no SQL files. `Formatter` owns mutable
formatting state; unlike the lexer/parser pair, use a separate formatter for
each concurrent operation. The null logger suppresses diagnostic output;
an application can supply its own logger instead.


## Load and Compare MySQL Definitions

Filesystem definition loading needs a larger graph. This example uses an existing
MySQL [project configuration](../app/configuration.md) and SQL definition files.
Configuration processing can contact the configured server to discover defaults,
just as it does during wrapper startup.

!!! note
    The comparison uses an empty starting definition, producing a create script from the definition files. It does **not** load or compare the live server's objects.

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.DefinitionMapping;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

var configFile = new FileInfo("/path/to/project/database-manager.yaml");
var rawConfig = StartupBase.LoadRawConfig(configFile)
    ?? throw new FileNotFoundException("Configuration file not found.", configFile.FullName);
var config = new MyStartup().BuildConfiguration(
    configFile.DirectoryName!, rawConfig, new EnvironmentVariableReader(),
    relaxed: false, logger: NullLogger.Instance);

var textParser = new TextParser(new MyLexer(), new MyParser());
var sources = new SourceManager();
var functionNames = new MyFunctionNameProvider();
var fileLoader = new DefinitionFileLoader<MySchemaMapping>(
    config,
    NullLogger<DefinitionFileLoader<MySchemaMapping>>.Instance);
var definitionLoader = new MyFsDefinitionLoader(
    config,
    fileLoader,
    textParser,
    sources,
    functionNames,
    NullLogger<MyFsDefinitionLoader>.Instance,
    NullLoggerFactory.Instance);

var desiredDefinition = definitionLoader.LoadDefinition(relaxed: false);
var startingDefinition = new MyDefinition();
var differ = new MyDiffer(
    config, startingDefinition, desiredDefinition,
    refactors: [], deployScripts: [], logger: NullLogger.Instance);
var changes = differ.ComputeChanges();

using var output = new StringWriter();
var writer = new DifferStatementWriter(config.DifferFormatting, output);
foreach (var change in changes)
{
    writer.WriteDefinitionAlter(change);
}
Console.WriteLine(output.ToString());
```

The same processed configuration is passed to both the file loader and definition
loader. `SourceManager` tracks source locations during definition building, while
the function-name provider supplies MySQL built-ins. `MyFsDefinitionLoader` also
needs a logger factory for the definition builders it constructs internally.

`MyDiffer` compares the two supplied `MyDefinition` objects. To compare against
a non-empty definition, replace `startingDefinition` with the definition you
loaded or constructed. This example passes empty refactor and deploy-script
collections; loading those for a live-server migration is separate work. Creating
the differ alone does not determine which scripts or refactors have already run.


## Where DI Saves Wiring

Direct construction does not initialize `AppServiceProvider` convenience
accessors, and none of these examples uses them. A manually constructed object
has no DI scope; the application owns its dependencies and disposes resources
such as formatters, writers, and database contexts itself.

The [DI and LibWrapper approaches](customization.md) register these dependencies,
provide matching provider services, and manage scoped database resources. The
underlying operations are the same; the difference is who assembles and owns the
object graph.
