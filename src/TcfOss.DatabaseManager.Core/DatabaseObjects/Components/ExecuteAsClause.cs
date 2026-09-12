using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

/// <summary>
/// T-SQL <c>EXECUTE AS</c> clause, used in <c>CREATE PROCEDURE</c>,
/// <c>CREATE FUNCTION</c>, and <c>CREATE TRIGGER</c> definitions to specify
/// the security context in which the routine executes.
/// </summary>
public abstract record ExecuteAsClause() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    /// <summary><c>EXECUTE AS CALLER</c>.</summary>
    public record Caller() : ExecuteAsClause
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("EXECUTE AS CALLER");
        }
    }

    /// <summary><c>EXECUTE AS SELF</c>.</summary>
    public record Self() : ExecuteAsClause
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("EXECUTE AS SELF");
        }
    }

    /// <summary><c>EXECUTE AS OWNER</c>.</summary>
    public record Owner() : ExecuteAsClause
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("EXECUTE AS OWNER");
        }
    }

    /// <summary><c>EXECUTE AS '<i>username</i>'</c>.</summary>
    public record User(Value.SingleQuotedString Username) : ExecuteAsClause
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"EXECUTE AS {Username}");
        }
    }
}
