using System.ComponentModel;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO.TypeConverters;

namespace TcfOss.DatabaseManager.Core.Common;

[TypeConverter(typeof(ColumnIdentifierTypeConverter))]
public record ColumnIdentifier(string Name, ObjectIdentifier Table, QuoteStyle QuoteStyle = QuoteStyle.None) : Identifier(Name, QuoteStyle)
{
    public static ColumnIdentifier FromObjectName(ObjectName name, ObjectIdentifier table, QuoteStyle? quoteStyle)
    {
        if (name.Values.Count == 4)
        {
            var givenTable = ObjectIdentifier.FromObjectName(new ObjectName([.. name.Values[..3]]), table.Schema, quoteStyle);
            return new ColumnIdentifier(name.Values[3].Name, givenTable, quoteStyle ?? name.Values[3].QuoteStyle) { OriginalIdentifiers = name.Values };
        }
        if (name.Values.Count == 3)
        {
            var givenTable = ObjectIdentifier.FromObjectName(new ObjectName([.. name.Values[..2]]), table.Schema, quoteStyle);
            return new ColumnIdentifier(name.Values[2].Name, givenTable, quoteStyle ?? name.Values[2].QuoteStyle) { OriginalIdentifiers = name.Values };
        }
        if (name.Values.Count == 2)
        {
            var givenTable = new ObjectIdentifier(name.Values[0].Name, table.Schema, quoteStyle ?? name.Values[0].QuoteStyle);
            return new ColumnIdentifier(name.Values[1].Name, givenTable, quoteStyle ?? name.Values[1].QuoteStyle) { OriginalIdentifiers = name.Values };
        }
        if (name.Values.Count == 1)
        {
            return new ColumnIdentifier(name.Values[0].Name, table, quoteStyle ?? name.Values[0].QuoteStyle) { OriginalIdentifiers = name.Values };
        }
        throw new IdentifierMismatchException.IdentifierLengthException("column", 4, name.Values.Count, name.Values);
    }

    public override string ToString()
    {
        return Table + "." + base.ToString();
    }

    public static ColumnIdentifier FromStrings(string catalog, string schema, string table, string column, QuoteStyle quoteStyle = QuoteStyle.None)
    {
        return new ColumnIdentifier(column,
            new ObjectIdentifier(table,
                new SchemaIdentifier(schema,
                    new CatalogIdentifier(catalog, quoteStyle),
                    quoteStyle),
                quoteStyle),
            quoteStyle);
    }
}
