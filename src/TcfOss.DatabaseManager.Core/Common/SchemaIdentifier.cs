using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.Common;

public record SchemaIdentifier(string Name, CatalogIdentifier Catalog, QuoteStyle QuoteStyle = QuoteStyle.None) : Identifier(Name, QuoteStyle)
{
    public static SchemaIdentifier FromObjectName(ObjectName name, CatalogIdentifier catalog, QuoteStyle? quoteStyle)
    {
        if (name.Values.Count == 2)
        {
            var givenCatalog = CatalogIdentifier.FromIdentifier(name.Values[0], quoteStyle);
            return new SchemaIdentifier(name.Values[1].Name, givenCatalog, quoteStyle ?? name.Values[1].QuoteStyle);
        }
        if (name.Values.Count == 1)
        {
            return new SchemaIdentifier(name.Values[0].Name, catalog, quoteStyle ?? name.Values[0].QuoteStyle);
        }
        throw new IdentifierMismatchException.IdentifierLengthException("schema", 2, name.Values.Count, name.Values);
    }

    public override string ToString()
    {
        return Catalog + "." + base.ToString();
    }
}
