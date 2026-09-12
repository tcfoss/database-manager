namespace TcfOss.DatabaseManager.Core.Common;

public record CatalogIdentifier(string Name, QuoteStyle QuoteStyle = QuoteStyle.None) : Identifier(Name, QuoteStyle)
{
    public static CatalogIdentifier FromIdentifier(Identifier identifier, QuoteStyle? quoteStyle)
    {
        return new CatalogIdentifier(identifier.Name, quoteStyle ?? identifier.QuoteStyle);
    }

    public override string ToString()
    {
        return base.ToString();
    }
}
