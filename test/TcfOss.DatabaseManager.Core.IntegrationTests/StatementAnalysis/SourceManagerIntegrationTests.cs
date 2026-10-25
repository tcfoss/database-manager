using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.StatementAnalysis;

public class SourceManagerIntegrationTests
{
    [Theory]
    [InlineData(2, 8, "SELECT")]
    [InlineData(0, 11, "  SELECT 1;")]
    [InlineData(2, 2, "")]
    [InlineData(11, 11, "")]
    public async Task GetText_Returns_Metadata_Range(int start, int end, string expected)
    {
        const string body = "  SELECT 1;";
        using var fixture = new FsProjectFixture();
        string path = fixture.CombinePath("source.sql");
        await fixture.WriteTextAsync("source.sql", body, TestContext.Current.CancellationToken);

        var manager = new SourceManager();
        int sourceId = manager.RegisterFileSource(path);
        var sourceRef = new SourceRef(sourceId, 0, body.Length);
        var meta = new MetaData { Start = start, End = end };

        Assert.Equal(expected, manager.GetText(sourceRef, meta));
        Assert.Equal(expected, SourceManager.GetText(body, meta));
    }
}
