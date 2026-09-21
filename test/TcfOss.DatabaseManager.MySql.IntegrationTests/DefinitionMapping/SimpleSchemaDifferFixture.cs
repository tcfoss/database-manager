using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.DefinitionMapping;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

public abstract class SimpleSchemaDifferFixture<TBuilderEntity, TContainerEntity> : IAsyncLifetime
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    private readonly FsProjectFixture _initialFsFixture = new();
    private readonly FsProjectFixture _endFsFixture = new();

    protected DbFixture<TBuilderEntity, TContainerEntity> DbFixture { get; init; } = null!;
    protected abstract SqlDialect Dialect { get; }
    public List<DefinitionAlterStatement> ActualChanges { get; } = [];

    public async ValueTask InitializeAsync()
    {
        await DbFixture.Container.StartAsync();

        string initializationScript = CommonHelpers.GetSimpleSchemaInitializationScript();
        await DbFixture.Container.ExecScriptAsync(initializationScript, TestContext.Current.CancellationToken);

        _initialFsFixture.CopyFiles(Path.Combine(CommonHelpers.SimpleSchemaSourceDirectory.FullName, "Initial"));
        _endFsFixture.CopyFiles(Path.Combine(CommonHelpers.SimpleSchemaSourceDirectory.FullName, "Test1"));

        var loggerFactory = new LoggerFactory();
        DbFixture.GetSimpleSchemaConfig(_initialFsFixture.RootDirectory.FullName, Dialect);
        var dbLoader = (MyDbDefinitionLoader)MyAppServiceProvider.DatabaseDefinitionLoader;
        MyDefinition startDefinition = await dbLoader.LoadDefinitionAsync();

        MyConfig config = DbFixture.GetSimpleSchemaConfig(_endFsFixture.RootDirectory.FullName, Dialect);
        List<DeployScript> deployScripts = LoadDeployScripts(config);
        var fsLoader = (MyFsDefinitionLoader)MyAppServiceProvider.FilesystemDefinitionLoader;
        MyDefinition endDefinition = fsLoader.LoadDefinition(false);

        var differ = new MyDiffer(
            config,
            startDefinition,
            endDefinition,
            [],
            deployScripts,
            loggerFactory.CreateLogger<MyDiffer>());
        ActualChanges.AddRange(differ.ComputeChanges());
    }

    private static List<DeployScript> LoadDeployScripts(MyConfig config)
    {
        var textParser = new TextParser(new MyLexer(), new MyParser());
        var deployScripts = new List<DeployScript>();

        foreach (MySchemaMapping schema in config.Schemas.Values)
        {
            foreach (Core.Configuration.DeployScript deployScript in schema.DeployScripts)
            {
                string bodyText = File.ReadAllText(deployScript.FilePath.FullName);
                SqlValueList<Statement> statements = textParser.ParseText(bodyText, deployScript.FilePath.FullName);
                foreach (Statement statement in statements)
                {
                    if (statement is IHaveBodyStatement haveBodyStatement)
                    {
                        statement.Meta.RawText = SourceManager.GetText(bodyText, haveBodyStatement.Body.Meta);
                    }
                    else
                    {
                        statement.Meta.RawText = SourceManager.GetText(bodyText, statement.Meta);
                    }
                }

                deployScripts.Add(new DeployScript(deployScript.Type, schema.SchemaName, deployScript.FileName, deployScript.FilePath.FullName, statements)
                {
                    UniqueId = deployScript.UniqueId,
                    RawBodyText = bodyText,
                });
            }
        }

        return deployScripts;
    }

    public async ValueTask DisposeAsync()
    {
        await DbFixture.Container.StopAsync();
        await DbFixture.Container.DisposeAsync();
        _initialFsFixture.Dispose();
        _endFsFixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
