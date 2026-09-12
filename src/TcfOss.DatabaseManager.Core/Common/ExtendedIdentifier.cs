using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Common;

public record ExtendedIdentifier(string Name, ExtendedQuoteStyle QuoteStyle = ExtendedQuoteStyle.None)
    : IWriteSql
{
    public SourceRef? Source { get; init; }

    private string ToSql(ExtendedQuoteStyle? quoteStyle)
    {
        if (quoteStyle != null)
        {
            return QuoteString(Name, quoteStyle.Value);
        }
        return QuoteString(Name, QuoteStyle);
    }

    public void ToSql(SqlTextWriter writer)
    {
        writer.Write(ToSql(QuoteStyle));
    }

    public override string ToString()
    {
        return ToSql(quoteStyle: null);
    }

    public static implicit operator ExtendedIdentifier(Identifier name)
    {
        ExtendedQuoteStyle newQuoteStyle = name.QuoteStyle switch
        {
            Common.QuoteStyle.Ansi => ExtendedQuoteStyle.Ansi,
            Common.QuoteStyle.Backticks => ExtendedQuoteStyle.Backticks,
            Common.QuoteStyle.Brackets => ExtendedQuoteStyle.Brackets,
            _ => ExtendedQuoteStyle.None
        };

        return new ExtendedIdentifier(name.Name, newQuoteStyle);
    }

    private static string QuoteString(string value, ExtendedQuoteStyle quoteStyle)
    {
        return quoteStyle switch
        {
            ExtendedQuoteStyle.Ansi => '"' + value + '"',
            ExtendedQuoteStyle.Backticks => $"`{value}`",
            ExtendedQuoteStyle.Brackets => $"[{value}]",
            ExtendedQuoteStyle.SingleQuote => $"'{value}'",
            _ => value,
        };
    }

    public ExtendedIdentifier WithQuoteStyle(QuoteStyle quoteStyle)
    {
        ExtendedQuoteStyle extendedQuoteStyle = quoteStyle switch
        {
            Common.QuoteStyle.Ansi => ExtendedQuoteStyle.Ansi,
            Common.QuoteStyle.Backticks => ExtendedQuoteStyle.Backticks,
            Common.QuoteStyle.Brackets => ExtendedQuoteStyle.Brackets,
            _ => ExtendedQuoteStyle.None
        };
        return this with { QuoteStyle = extendedQuoteStyle };
    }

    public virtual bool Equals(ExtendedIdentifier? other)
    {
        if (other == null)
        {
            return false;
        }
        return Name == other.Name && QuoteStyle == other.QuoteStyle;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, QuoteStyle);
    }
}
