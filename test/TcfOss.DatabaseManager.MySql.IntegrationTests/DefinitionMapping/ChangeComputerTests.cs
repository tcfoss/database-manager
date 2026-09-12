using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MySql.Configuration;
using Testcontainers.Xunit;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

public abstract class ChangeComputerTests<TBuilderEntity, TContainerEntity>(ITestOutputHelper testOutputHelper)
    : ContainerTest<TBuilderEntity, TContainerEntity>(testOutputHelper)
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IDatabaseContainer
{
    protected abstract SqlDialect Dialect { get; }

    [Fact]
    public async Task Test_Compute_Apply_Compute_No_More_Changes_1()
    {
        await DoWork("Test1", true, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Test_Compute_Apply_Compute_No_More_Changes_2()
    {
        await DoWork("Test2", false, true, TestContext.Current.CancellationToken);
    }

    private async Task DoWork(string sourceDirectoryComponent, bool includeScripts, bool includeRefactors, CancellationToken ct = default)
    {
        await Container.InitializeLibrarySchema();

        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory(sourceDirectoryComponent).FullName);

        _ = GetConfig(fileFixture, includeScripts, includeRefactors);

        var changesPath = Path.Combine(fileFixture.RootDirectory.FullName, "Changes.sql");
        var computer = Core.App.AppServiceProvider.ChangeComputer;

        await computer.ExecuteAsync(changesPath, FileExistsAction.Error, ct);

        Assert.True(File.Exists(changesPath));

        var body = await File.ReadAllTextAsync(changesPath, ct);
        Assert.False(string.IsNullOrWhiteSpace(body));

        var result = ExecuteDbScriptInContainer(body, ct);
        Assert.Equal(0, result.ExitCode);

        var newChangesPath = Path.Combine(fileFixture.RootDirectory.FullName, "NewChanges.sql");
        await computer.ExecuteAsync(newChangesPath, FileExistsAction.Error, ct);

        Assert.True(File.Exists(newChangesPath));

        var newBody = await File.ReadAllTextAsync(newChangesPath, ct);
        Assert.True(string.IsNullOrWhiteSpace(newBody));
    }

    private ExecResult ExecuteDbScriptInContainer(string script, CancellationToken ct)
    {
        return Container.ExecScriptAsync(script, ct).GetAwaiter().GetResult();
    }

    private MyConfig GetConfig(FsProjectFixture fsFixture, bool includeScripts, bool includeRefactors)
    {
        var rootPath = fsFixture.RootDirectory.FullName;
        return Container.GetLibrarySchemaConfig(rootPath, includeScripts, includeRefactors, Container.GetPort(), Dialect, true);
    }
}
