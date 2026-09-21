using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;
using TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

namespace TcfOss.DatabaseManager.MySql.Tests.App;

public class MyCommandRunnerTests
{
    [Fact]
    public void ParseDefinition_NoConfig_Throws()
    {
        var runner = GetRunner();

        Assert.Throws<CommandException.ConfigurationRequired>(() =>
        {
            runner.ParseDefinition("/tmp/output.json");
        });
    }

    [Fact]
    public async Task DownloadSchema_NoConfig_Throws()
    {
        var runner = GetRunner();

        await Assert.ThrowsAsync<CommandException.ConfigurationRequired>(async () =>
        {
            await runner.DownloadSchemaAsync();
        });
    }

    [Fact]
    public async Task ComputeChanges_NoConfig_Throws()
    {
        var runner = GetRunner();

        await Assert.ThrowsAsync<CommandException.ConfigurationRequired>(async () =>
        {
            await runner.ComputeChangesAsync(null);
        });
    }

    private static MyCommandRunner GetRunner(MyConfig? config = null, MyConfig? configForRunner = null)
    {
        var factory = new LoggerFactory();
        config ??= GetConfig();

        var parser = new MyParser();
        var lexer = new MyLexer();
        var textParser = new TextParser(lexer, parser);
        var reader = new DefinitionFileLoader<MySchemaMapping>(config, factory.CreateLogger<DefinitionFileLoader<MySchemaMapping>>());
        var writer = new FileWriter(factory.CreateLogger<FileWriter>());
        var contextFixture = new MyInfoSchemaContextFixture();
        var contextFactory = contextFixture.CreateFactory();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(sc =>
        {
            sc.AddProvider(NullLoggerProvider.Instance);
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();
        AppServiceProvider.ServiceProvider = serviceProvider;

        var fsLoader = new MyFsDefinitionLoader(
            config,
            reader,
            textParser,
            new SourceManager(),
            new MyFunctionNameProvider(),
            AppServiceProvider.GetLogger<MyFsDefinitionLoader>(),
            factory
        );
        var dbLoader = new MyDbDefinitionLoader(
            config,
            new InfoSchemaRepo(config, contextFactory, new FakeRawEntityRetriever()),
            textParser,
            new MyFunctionNameProvider(),
            AppServiceProvider.GetLogger<MyDbDefinitionLoader>()
        );
        var formatter = new MySqlFormatter(
            config,
            writer,
            textParser,
            new MyFunctionNameProvider(),
            fsLoader,
            AppServiceProvider.GetLogger<MySqlFormatter>());

        return new MyCommandRunner(
            configForRunner!,
            parser,
            lexer,
            formatter,
            fsLoader,
            new MyDownloadSchema(config, factory.CreateLogger<MyDownloadSchema>(), dbLoader),
            writer,
            new MyChangeComputer(config, writer, AppServiceProvider.ScopeFactory, dbLoader, dbLoader, factory.CreateLogger<MyChangeComputer>()),
            new SourceManager(),
            factory.CreateLogger<MyCommandRunner>());
    }

    private static MyConfig GetConfig()
    {
        return TestConfig.GetMyTestConfig();
    }
}
