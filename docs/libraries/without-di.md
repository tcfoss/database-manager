---
description: Construct DatabaseManager parsers, formatters, definition loaders, and comparison objects directly without a DI container or host.
---

# Without Dependency Injection

The underlying libraries can be used without a host, service provider, or
hosting integration. Construct the objects directly and supply their dependencies.
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
`GenericConfigurationBuilder` prepares configuration. This complete generic example uses defaults and does not contact a
server:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

var workingDirectory = Directory.GetCurrentDirectory();
var configurationBuilder = new GenericConfigurationBuilder();
var rawConfig = configurationBuilder.GetDefaultConfig(workingDirectory);
var config = configurationBuilder.BuildConfiguration(
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

## Configuration Files

`ConfigurationFile`, in `TcfOss.DatabaseManager.Core.Configuration`, handles
discovery and raw YAML loading. To find a configuration
from a working directory, call
`ConfigurationFile.GetConfigPath(workingDirectory, configPath: null)`. Discovery
searches that directory and its parents using the same filenames as the CLI.
An explicit `configPath` is resolved relative to the working directory instead.

Pass the returned `FileInfo` to `ConfigurationFile.LoadRawConfig`. It returns
`null` for a missing file; the caller chooses whether to throw or use
`GetDefaultConfig`. Pass the raw configuration to the matching builder:

| Base package | Configuration builder | Processed configuration |
| --- | --- | --- |
| `Core` | `GenericConfigurationBuilder` | `ConfigGeneric` |
| `MySql` | `MyConfigurationBuilder` | `MyConfig` |
| `MariaDb` | `MaConfigurationBuilder` | `MyConfig` |
| `MsSql` | `MsConfigurationBuilder` | `MsConfig` |

Each builder lives in its base package's `.Configuration` namespace. Builders
prepare defaults, substitute credential environment variables where applicable,
and invoke the configuration loaders. MySQL/MariaDB preparation can contact a
server to discover defaults; without a connection it retains the existing configured/default
fallback behavior.


## Load and Compare MySQL Definitions

Filesystem definition loading needs a larger graph. This example uses an existing
MySQL [project configuration](../app/configuration.md) and SQL definition files.
Configuration processing can contact the configured server to discover defaults,
just as it does during CLI startup. Only `TcfOss.DatabaseManager.MySql` is needed;
it includes configuration preparation and brings Core transitively.

!!! note
    The comparison uses an empty starting definition, producing a create script from the definition files. It does **not** load or compare the live server's objects.

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.DefinitionMapping;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

var configFile = new FileInfo("/path/to/project/database-manager.yaml");
var rawConfig = ConfigurationFile.LoadRawConfig(configFile)
    ?? throw new FileNotFoundException("Configuration file not found.", configFile.FullName);
var config = new MyConfigurationBuilder().BuildConfiguration(
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

The biggest advantage to a [hosted approach](customization.md) is that it spares you from
having to manually construct and supply dependencies to every object you need and partially
insulates you from constructor signature changes.

