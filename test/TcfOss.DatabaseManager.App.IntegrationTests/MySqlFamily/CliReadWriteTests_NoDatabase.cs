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
        using var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("Test1").FullName);

        var workingDir = fileFixture.RootDirectory.FullName;

        UpdateConfiguration(workingDir, null, null);

        var diffResult = CliRunner.RunCli("compute-changes", fileFixture, out _, out var actual, "InitialChanges.sql");
        Assert.Equal(1, diffResult);

        Assert.Equal(string.Format(MessageTemplates.ConnectionRequiredTemplate, "compute-changes"), actual);
        return Task.CompletedTask;
    }

    [Fact]
    public override Task Verify_DoubleDiff_NoChanges_2()
    {
        using var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("Test2").FullName);

        var workingDir = fileFixture.RootDirectory.FullName;

        UpdateConfiguration(workingDir, null, null);

        var diffResult = CliRunner.RunCli("compute-changes", fileFixture, out _, out var actual, "InitialChanges.sql");

        Assert.Equal(1, diffResult);

        Assert.Equal(string.Format(MessageTemplates.ConnectionRequiredTemplate, "compute-changes"), actual);

        return Task.CompletedTask;
    }

    [Fact]
    public override Task Verify_DoubleDiff_NoChanges_SimpleSchema()
    {
        using var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("SimpleSchema", "Test1").FullName);

        var workingDir = fileFixture.RootDirectory.FullName;

        UpdateConfiguration(workingDir, null, null);

        var diffResult = CliRunner.RunCli("compute-changes", fileFixture, out _, out var actual, "InitialChanges.sql");

        Assert.Equal(1, diffResult);

        Assert.Equal(string.Format(MessageTemplates.ConnectionRequiredTemplate, "compute-changes"), actual);

        return Task.CompletedTask;
    }

    [Fact]
    public override Task Verify_SameSchema_NoChanges_SimpleSchema()
    {
        using var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("SimpleSchema", "Initial").FullName);

        var workingDir = fileFixture.RootDirectory.FullName;

        UpdateConfiguration(workingDir, null, null);

        var diffResult = CliRunner.RunCli("compute-changes", fileFixture, out _, out var actual);
        Assert.Equal(1, diffResult);
        Assert.Equal(string.Format(MessageTemplates.ConnectionRequiredTemplate, "compute-changes"), actual);

        return Task.CompletedTask;
    }
}
