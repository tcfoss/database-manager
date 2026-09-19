using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.MySql.IntegrationTests;


namespace TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;

public abstract class CliReadWriteTests_NoDatabase(ITestOutputHelper testOutputHelper) : CliReadWriteTests<FakeDbBuilder, FakeDbContainer>(testOutputHelper)
{
    [Fact]
    public override Task Verify_DoubleDiff_NoChanges_1()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("Test1").FullName);

        var workingDir = fileFixture.RootDirectory.FullName;

        UpdateConfiguration(workingDir, null, null);

        var cli = new CommandLineInterface();
        var diffResult = cli.Run([
            "--working-dir",
            workingDir,
            "compute-changes",
            "InitialChanges.sql"
        ]);

        Assert.Equal(1, diffResult);

        var actual = GetOutputLines(TestOutputHelper);
        Assert.Equal(string.Format(MessageTemplates.ConnectionRequiredTemplate, "compute-changes"), actual);
        return Task.CompletedTask;
    }

    [Fact]
    public override Task Verify_DoubleDiff_NoChanges_2()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("Test2").FullName);

        var workingDir = fileFixture.RootDirectory.FullName;

        UpdateConfiguration(workingDir, null, null);

        var cli = new CommandLineInterface();
        var diffResult = cli.Run([
            "--working-dir",
            workingDir,
            "compute-changes",
            "InitialChanges.sql"
        ]);

        Assert.Equal(1, diffResult);

        var actual = GetOutputLines(TestOutputHelper);
        Assert.Equal(string.Format(MessageTemplates.ConnectionRequiredTemplate, "compute-changes"), actual);

        return Task.CompletedTask;
    }

    [Fact]
    public override Task Verify_DoubleDiff_NoChanges_SimpleSchema()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("SimpleSchema", "Test1").FullName);

        var workingDir = fileFixture.RootDirectory.FullName;

        UpdateConfiguration(workingDir, null, null);

        var cli = new CommandLineInterface();
        var diffResult = cli.Run([
            "--working-dir",
            workingDir,
            "compute-changes",
            "InitialChanges.sql"
        ]);

        Assert.Equal(1, diffResult);

        var actual = GetOutputLines(TestOutputHelper);
        Assert.Equal(string.Format(MessageTemplates.ConnectionRequiredTemplate, "compute-changes"), actual);

        return Task.CompletedTask;
    }
}
