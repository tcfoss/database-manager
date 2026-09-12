using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.MySql.IntegrationTests;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;

public abstract class CliReadOnlyTests_NoDatabase : CliReadOnlyTests<CliReadOnlyFixture_NoDatabase>
{
    protected override bool ConnectionAvailable => false;

    protected CliReadOnlyTests_NoDatabase(CliReadOnlyFixture_NoDatabase fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }

    [Fact]
    public override Task DownloadSchema()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName, "database-manager.yaml"));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var downloadResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "download-schema",
        ]);

        Assert.NotEqual(0, downloadResult);

        var actual = GetOutputLines(TestOutputHelper);
        Assert.Equal(string.Format(MessageTemplates.ConnectionRequiredTemplate, "download-schema"), actual);

        return Task.CompletedTask;
    }
}
