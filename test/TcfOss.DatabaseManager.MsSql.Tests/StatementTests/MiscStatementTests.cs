using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.MsSql.Statements;

namespace TcfOss.DatabaseManager.MsSql.Tests.StatementTests;

public class MiscStatementTests
{
    [Theory]
    [InlineData(null, "GO")]
    [InlineData(1, "GO")]
    [InlineData(2, "GO 2")]
    public void BatchSeparator_ToSql(int? count, string expected)
    {
        MsBatchSeparator stmt = count.HasValue ? new MsBatchSeparator(count.Value) : new MsBatchSeparator();
        Assert.Equal(expected, stmt.ToSql());
    }

    [Fact]
    public void BatchSeparator_NoReferencedItems()
    {
        MsBatchSeparator stmt = new(5);
        Assert.Empty(stmt.GetReferencedItems(new ReferencedItemsManager()));
    }
}
