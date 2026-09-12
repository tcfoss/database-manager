using System.ComponentModel;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.IO.TypeConverters;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Common;

[TypeConverter(typeof(IdentifierTypeConverter))]
public record Identifier(string Name, QuoteStyle QuoteStyle = QuoteStyle.None, SigilKind Sigil = SigilKind.None)
    : IWriteSql
{
    public SourceRef? Source { get; init; }
    public SqlValueList<Identifier>? OriginalIdentifiers { get; init; }

    public Identifier ToSimpleIdentifier(QuoteStyle? quoteStyle = null)
    {
        return new Identifier(Name, quoteStyle ?? QuoteStyle, Sigil)
        {
            Source = Source,
            OriginalIdentifiers = OriginalIdentifiers ?? [this]
        };
    }

    private string ToSql(QuoteStyle? quoteStyle)
    {
        return QuoteString(Name, quoteStyle ?? QuoteStyle, Sigil);
    }

    public void ToSql(SqlTextWriter writer)
    {
        writer.Write(ToSql(QuoteStyle));
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write(manager.GetQuotedIdentifier(this));
    }

    public override string ToString()
    {
        return ToSql(quoteStyle: null);
    }

    public static implicit operator string(Identifier name)
    {
        return name.ToString();
    }

    public static string QuoteString(string value, QuoteStyle quoteStyle, SigilKind sigil)
    {
        string quoted = quoteStyle switch
        {
            QuoteStyle.Ansi => '"' + value + '"',
            QuoteStyle.Backticks => $"`{value}`",
            QuoteStyle.Brackets => $"[{value}]",
            _ => value,
        };
        return sigil switch
        {
            SigilKind.Variable => "@" + quoted,
            _ => quoted,
        };
    }

    public virtual bool Equals(Identifier? other)
    {
        if (other == null)
        {
            return false;
        }
        return Name == other.Name && QuoteStyle == other.QuoteStyle && Sigil == other.Sigil;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, QuoteStyle, Sigil);
    }
}
