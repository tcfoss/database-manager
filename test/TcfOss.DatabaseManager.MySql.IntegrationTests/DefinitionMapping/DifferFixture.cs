using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.DefinitionMapping;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

public class DifferFixture<TBuilderEntity, TContainerEntity> : IAsyncLifetime
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    private readonly FsProjectFixture _fsFixtureInitial = new();
    protected DbFixture<TBuilderEntity, TContainerEntity> DbFixture { get; init; } = null!;
    private MyConfig MyConfig { get; set; } = null!;
    protected virtual SqlDialect Dialect => SqlDialect.MySql;
    protected virtual bool RemoveSlashesBeforeQuotesGenerationExpression => false;

    public List<TestChangeConfig> TestSets { get; } = [
        new() {
            SourcePath = new DirectoryInfo(Path.Combine(CommonDirectoryPath.GetProjectDirectory().DirectoryPath, "..", "Resources", "TestSchemas", "MySql", "Initial")).FullName
        },
        new() {
            SourcePath = new DirectoryInfo(Path.Combine(CommonDirectoryPath.GetProjectDirectory().DirectoryPath, "..", "Resources", "TestSchemas", "MySql", "Test1")).FullName,
            IncludeScripts = true,
        },
        new() {
            SourcePath = new DirectoryInfo(Path.Combine(CommonDirectoryPath.GetProjectDirectory().DirectoryPath, "..", "Resources", "TestSchemas", "MySql", "Test2")).FullName,
            IncludeRefactors = true,
        }
    ];

    public virtual async ValueTask InitializeAsync()
    {
        var loggerFactory = new LoggerFactory();

        var startDef = await GetStartDefinition();

        foreach (var testSet in TestSets)
        {
            var endDef = GetEndDefinition(testSet);

            // WriteSerializedJson(startDef, endDef);
            var refactors = await AppServiceProvider.RefactorLoader.GetAllUnappliedRefactorsAsync(MyConfig.Schemas.Keys);

            var differ = new MyDiffer(
                config: MyConfig,
                start: startDef,
                end: endDef,
                refactors: refactors,
                deployScripts: [],
                logger: loggerFactory.CreateLogger<MyDiffer>()
            );

            var actualChanges = differ.ComputeChanges();

            // Collapse weights to get deterministic ordering for tests.
            for (int i = 0; i < actualChanges.Count; i++)
            {
                var change = actualChanges[i];
                if (change.Weight is >= DefaultWeights.AlterTable and < DefaultWeights.CreateTable)
                {
                    actualChanges[i] = change with { Weight = DefaultWeights.AlterTable };
                }
            }
            testSet.ActualChanges = actualChanges;
        }
    }

    private async Task<MyDefinition> GetStartDefinition()
    {
        await DbFixture.Container.StartAsync();
        DbFixture.InitializeLibrarySchema();
        MyConfig = DbFixture.GetLibrarySchemaConfig(_fsFixtureInitial.RootDirectory.FullName, false, false, Dialect, RemoveSlashesBeforeQuotesGenerationExpression);
        var dbLoader = (MyDbDefinitionLoader)MyAppServiceProvider.DatabaseDefinitionLoader;

        return await dbLoader.LoadDefinitionAsync();
    }

    private MyDefinition GetEndDefinition(TestChangeConfig testSet)
    {
        testSet.FsFixture.CopyFiles(testSet.SourcePath, testSet.FsFixture.RootDirectory.FullName);
        MyConfig = DbFixture.GetLibrarySchemaConfig(testSet.FsFixture.RootDirectory.FullName, testSet.IncludeScripts, testSet.IncludeRefactors, Dialect, RemoveSlashesBeforeQuotesGenerationExpression);
        var fsLoader = (MyFsDefinitionLoader)MyAppServiceProvider.FilesystemDefinitionLoader;

        return fsLoader.LoadDefinition(false);
    }

    // public static void WriteSerializedJson(MyDefinition startDef, MyDefinition endDef)
    // {
    //     var writer = new FileWriter(new LoggerFactory().CreateLogger<FileWriter>());
    //     var startJson = Serialization.ToJson(DefinitionHelpers.SortDefinition(startDef), new Core.Configuration.SerializationSettings
    //     {
    //         OmitMetaData = true,
    //         OmitPreNonSql = true,
    //         OmitRawText = true,
    //     });
    //     writer.WriteTextToFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "start.json"), startJson, FileExistsAction.Overwrite);
    //     var endJson = Serialization.ToJson(DefinitionHelpers.SortDefinition(endDef), new Core.Configuration.SerializationSettings
    //     {
    //         OmitMetaData = true,
    //         OmitPreNonSql = true,
    //         OmitRawText = true,
    //     });
    //     writer.WriteTextToFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "end.json"), endJson, FileExistsAction.Overwrite);
    // }

    public async ValueTask DisposeAsync()
    {
        await DbFixture.Container.StopAsync();
        await DbFixture.Container.DisposeAsync();
        _fsFixtureInitial.Dispose();
        GC.SuppressFinalize(this);
    }

    public class TestChangeConfig
    {
        public FsProjectFixture FsFixture { get; } = new();
        public string SourcePath { get; init; } = "";
        public bool IncludeScripts { get; init; }
        public bool IncludeRefactors { get; init; }
        public List<DefinitionAlterStatement> ActualChanges { get; set; } = null!;
    }
}
