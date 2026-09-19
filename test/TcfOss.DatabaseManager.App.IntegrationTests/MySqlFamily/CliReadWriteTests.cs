using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests;
using Testcontainers.Xunit;

[assembly: CaptureConsole(CaptureOut = false)]

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;

public abstract class CliReadWriteTests<TBuilderEntity, TContainerEntity>(ITestOutputHelper testOutputHelper)
    : ContainerTest<TBuilderEntity, TContainerEntity>(testOutputHelper)
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IDatabaseContainer
{
    protected ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;
    protected abstract SqlDialect Dialect { get; }

    [Fact]
    public virtual async Task Verify_DoubleDiff_NoChanges_1()
    {
        await Container.InitializeLibrarySchema();

        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("Test1").FullName);

        var initialChangesText = await Verify_DoubleDiff_NoChanges(fileFixture.RootDirectory.FullName, Container);

        Assert.Contains("INSERT INTO acquisition_initial_data", initialChangesText);

        Assert.Matches(@"INSERT INTO (`library_catalog`\.)?`_database_manager` \(`entry_key`, `entry_type`\) VALUES \('00000000-0000-0000-0000-000000000010', 'D'\), \('00000000-0000-0000-0000-000000000011', 'D'\);", initialChangesText);
        Assert.Matches(@"INSERT INTO (`library_catalog`\.)?`_database_manager` \(`entry_key`, `entry_type`\) VALUES \('00000000-0000-0000-0000-000000000012', 'D'\), \('00000000-0000-0000-0000-000000000013', 'D'\), \('00000000-0000-0000-0000-000000000014', 'D'\);", initialChangesText);

        var execResult = await Container.ExecScriptAsync("SELECT COUNT(*) FROM library_catalog.item;", TestContext.Current.CancellationToken);
        Assert.Equal(0, execResult.ExitCode);
        var count = execResult.Stdout.Split("\n")[1].Trim();
        Assert.Equal("6", count);
    }

    [Fact]
    public virtual async Task Verify_DoubleDiff_NoChanges_2()
    {
        await Container.InitializeLibrarySchema();

        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("Test2").FullName);

        var initialChangesText = await Verify_DoubleDiff_NoChanges(fileFixture.RootDirectory.FullName, Container);

        Assert.Matches(@"INSERT INTO (`library_catalog`\.)?`_database_manager` \(`entry_key`, `entry_type`\) VALUES \('f3e7e2b8-4e3e-4c8a-8b2e-6b2e9e3b7c1d', 'R'\), \('a1b2c3d4-5e6f-7g8h-9i0j-k1l2m3n4o5p6', 'R'\);", initialChangesText);
        Assert.Matches(@"INSERT INTO (`library_activity`\.)?`_database_manager` \(`entry_key`, `entry_type`\) VALUES \('7a8b9c0d-e1f2-3g4h-5i6j-7k8l9m0n1o2p', 'R'\), \('2b3c4d5e-6f7g-8h9i-0j1k-l2m3n4o5p6q7', 'R'\), \('3c4d5e6f-7g8h-9i0j-1k2l-m3n4o5p6q7r8', 'R'\);", initialChangesText);
    }

    [Fact]
    public virtual async Task Verify_DoubleDiff_NoChanges_SimpleSchema()
    {
        string initializationScript = CommonHelpers.GetSimpleSchemaInitializationScript();
        await Container.ExecScriptAsync(initializationScript, TestContext.Current.CancellationToken);

        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("SimpleSchema", "Test1").FullName);

        string initialChangesText = await Verify_DoubleDiff_NoChanges(fileFixture.RootDirectory.FullName, Container);

        Assert.Contains("ALTER TABLE `samples` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci", initialChangesText);
        Assert.Contains("ALTER TABLE `widgets` DEFAULT CHARACTER SET latin1 COLLATE latin1_swedish_ci", initialChangesText);
        Assert.Contains("MODIFY COLUMN `changed_collation`", initialChangesText);
        Assert.Contains("MODIFY COLUMN `changed_charset`", initialChangesText);
    }

    protected async Task<string> Verify_DoubleDiff_NoChanges(string workingDir, TContainerEntity container)
    {
        UpdateConfiguration(workingDir, container.Hostname, container.GetMappedPublicPort(container.GetPort()));

        var cli = new CommandLineInterface();
        var diffResult = cli.Run([
            "--working-dir",
            workingDir,
            "compute-changes",
            "InitialChanges.sql"
        ]);

        Assert.Equal(0, diffResult);

        var changesPath = Path.Combine(workingDir, "InitialChanges.sql");
        Assert.True(File.Exists(changesPath), "Changes file was not created.");

        var changesText = await File.ReadAllTextAsync(changesPath, TestContext.Current.CancellationToken);

        var execResult = await container.ExecScriptAsync(changesText, TestContext.Current.CancellationToken);
        Assert.Equal(0, execResult.ExitCode);

        var verifyResult = cli.Run([
            "--working-dir",
            workingDir,
            "compute-changes",
            "PostChanges.sql"
        ]);
        Assert.Equal(0, verifyResult);

        var newChangesPath = Path.Combine(workingDir, "PostChanges.sql");
        Assert.True(File.Exists(newChangesPath), "Post changes file was not created.");
        var newChangesText = await File.ReadAllTextAsync(newChangesPath, TestContext.Current.CancellationToken);
        Assert.True(string.IsNullOrWhiteSpace(newChangesText), "There should be no new changes.");

        return changesText;
    }


    protected void UpdateConfiguration(string dirPath, string? host, ushort? port)
    {
        CommonHelpers.UpdateConfiguration(Dialect, dirPath, host, port);
    }


    protected static string GetOutputLines(ITestOutputHelper testOutputHelper)
    {
        var lines = testOutputHelper.Output.Split(Environment.NewLine);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].StartsWith("[testcontainers"))
            {
                return string.Join(Environment.NewLine, lines.Skip(i + 1)).Trim();
            }
        }
        return string.Join(Environment.NewLine, lines).Trim();
    }
}
