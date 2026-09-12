using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Common;

public abstract record Account() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public record CurrentUser() : Account
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("CURRENT_USER");
        }
    }

    public record CurrentRole() : Account
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("CURRENT_ROLE");
        }
    }

    public record SessionUser() : Account
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SESSION_USER");
        }
    }

    public record Identity(ExtendedIdentifier Name) : Account
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name}");
        }
    }

    public record IdentityWithHost(ExtendedIdentifier Name, ExtendedIdentifier Host) : Account
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name}@{Host}");
        }
    }
}
