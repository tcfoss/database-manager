using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.MySql.Statements;

namespace TcfOss.DatabaseManager.MySql.Tests.StatementTests;

public class MiscStatementTests
{
    [Fact]
    public void SetDelimiter_ToSql()
    {
        var sd = new SetDelimiter("//");
        Assert.Equal("DELIMITER //", sd.ToSql());
    }

    [Fact]
    public void SetDelimiter_NoReferencedItems()
    {
        var sd = new SetDelimiter("//");
        Assert.Empty(sd.GetReferencedItems(new ReferencedItemsManager()));
    }
}
