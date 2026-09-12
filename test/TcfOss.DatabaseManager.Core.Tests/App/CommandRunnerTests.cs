using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.App;

public class CommandRunnerTests
{
    [Fact]
    public void ParseDefinitionThrows()
    {
        var config = GetConfig();
        var (writer, formatter) = GetFileWriterAndFormatter(config);
        var runner = new CommandRunner<ConfigBase>(config, writer, formatter);

        Assert.Throws<CommandException.SpecificDialectRequired>(() =>
        {
            runner.ParseDefinition("/tmp/output.json");
        });
    }

    [Fact]
    public async Task DownloadSchemaThrows()
    {
        var config = GetConfig();
        var (writer, formatter) = GetFileWriterAndFormatter(config);
        var runner = new CommandRunner<ConfigBase>(config, writer, formatter);

        await Assert.ThrowsAsync<CommandException.SpecificDialectRequired>(async () =>
        {
            await runner.DownloadSchemaAsync();
        });
    }

    [Fact]
    public async Task ComputeChangesThrows()
    {
        var config = GetConfig();
        var (writer, formatter) = GetFileWriterAndFormatter(config);
        var runner = new CommandRunner<ConfigBase>(config, writer, formatter);

        await Assert.ThrowsAsync<CommandException.SpecificDialectRequired>(async () =>
        {
            await runner.ComputeChangesAsync(null);
        });
    }

    private static ConfigBase GetConfig()
    {
        return new ConfigBase()
        {
            ProjectDirectory = "/tmp/project",
            Catalog = new CatalogIdentifier("TestCatalog"),
            Dialect = SqlDialect.Generic,
            QuoteStyle = QuoteStyle.Ansi,
            ValidationSettings = new ValidationSettings(),
            DatabaseAvailable = false
        };
    }

    private static (FileWriter writer, SqlFormatter<ConfigBase> formatter) GetFileWriterAndFormatter(ConfigBase config)
    {
        var writer = new FileWriter(new LoggerFactory().CreateLogger<FileWriter>());
        var formatter = new SqlFormatter<ConfigBase>(
            config,
            writer,
            new TextParser(new Lexer<RunState>(), new Parser()),
            new FunctionNameProvider(),
            new LoggerFactory().CreateLogger<SqlFormatter<ConfigBase>>());
        return (writer, formatter);
    }
}
