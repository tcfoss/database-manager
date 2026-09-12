using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Formatting;

// Interface also provided for library usage
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable UnusedMember.Global
public interface IFormatSql
{
    public void Format(Statement statement);

    public void Format(string sqlText);

    public string GetFormatted(Statement statement);

    public string GetFormatted(string? sqlText = null);
}
