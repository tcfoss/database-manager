using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.MySql.IntegrationTests;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;

public abstract class CliReadOnlyTests_NoDatabase : CliReadOnlyTests<CliReadOnlyFixture_NoDatabase>
{
    protected override bool ConnectionAvailable => false;

    protected CliReadOnlyTests_NoDatabase(CliReadOnlyFixture_NoDatabase fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public override Task DownloadSchema()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName, "database-manager.yaml"));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var downloadResult = CliRunner.RunCli("download-schema", fileFixture, out _, out var actual);

        Assert.NotEqual(0, downloadResult);

        Assert.Equal(string.Format(MessageTemplates.ConnectionRequiredTemplate, "download-schema"), actual);

        return Task.CompletedTask;
    }

    public override Task ComputeChanges_SyntaxError_Fails()
    {
        return Task.CompletedTask;
    }

    public override Task ComputeChanges_ErrorMode_FileAlreadyExists_Fails()
    {
        return Task.CompletedTask;
    }

    public override Task ComputeChanges_SkipMode_FileAlreadyExists_DoesNothing()
    {
        return Task.CompletedTask;
    }
}
