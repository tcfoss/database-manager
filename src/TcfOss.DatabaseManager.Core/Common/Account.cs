using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Common;

public abstract record Account() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

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

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            if (Name.QuoteStyle == ExtendedQuoteStyle.SingleQuote)
            {
                ToSql(writer);
                return;
            }

            QuoteStyle quoteStyle = Name.QuoteStyle switch
            {
                ExtendedQuoteStyle.Ansi => QuoteStyle.Ansi,
                ExtendedQuoteStyle.Backticks => QuoteStyle.Backticks,
                ExtendedQuoteStyle.Brackets => QuoteStyle.Brackets,
                _ => QuoteStyle.None
            };
            var identifier = new Identifier(Name.Name, quoteStyle) { Source = Name.Source };
            identifier.FormatSql(writer, manager);
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
