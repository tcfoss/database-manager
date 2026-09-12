using System.ComponentModel;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO.TypeConverters;

namespace TcfOss.DatabaseManager.Core.Common;

[TypeConverter(typeof(ObjectIdentifierTypeConverter))]
public record ObjectIdentifier(string Name, SchemaIdentifier Schema, QuoteStyle QuoteStyle = QuoteStyle.None) : Identifier(Name, QuoteStyle)
{
    public (string Catalog, string Schema, string Name) Strings => (Schema.Catalog.Name, Schema.Name, Name);

    public ObjectName ToObjectName(int depth)
    {
        if (depth == 1)
        {
            return new ObjectName([ToSimpleIdentifier()]);
        }
        if (depth == 2)
        {
            return new ObjectName([Schema.ToSimpleIdentifier(), ToSimpleIdentifier()]);
        }
        if (depth == 3)
        {
            return new ObjectName([Schema.Catalog.ToSimpleIdentifier(), Schema.ToSimpleIdentifier(), ToSimpleIdentifier()]);
        }
        throw new IdentifierMismatchException.IdentifierLengthException("{ table | view | procedure | function | trigger | event }", 3, depth, [Schema.Catalog.ToSimpleIdentifier(), Schema.ToSimpleIdentifier(), ToSimpleIdentifier()]);
    }

    public static ObjectIdentifier FromObjectName(ObjectName name, SchemaIdentifier schema, QuoteStyle? quoteStyle)
    {
        if (name.Values.Count == 3)
        {
            // [0:catalog] . [1:schema] . [2:object]
            var givenSchema = SchemaIdentifier.FromObjectName(new ObjectName([.. name.Values[..2]]), schema.Catalog, quoteStyle);
            return new ObjectIdentifier(name.Values[2].Name, givenSchema, quoteStyle ?? name.Values[2].QuoteStyle) { OriginalIdentifiers = name.Values };
        }
        if (name.Values.Count == 2)
        {
            // [0:schema] . [1:object]
            var givenSchema = SchemaIdentifier.FromObjectName(new ObjectName([name.Values[0]]), schema.Catalog, quoteStyle);
            return new ObjectIdentifier(name.Values[1].Name, givenSchema, quoteStyle ?? name.Values[1].QuoteStyle) { OriginalIdentifiers = name.Values };
        }
        if (name.Values.Count == 1)
        {
            // [0:object]
            return new ObjectIdentifier(name.Values[0].Name, schema, quoteStyle ?? name.Values[0].QuoteStyle) { OriginalIdentifiers = name.Values };
        }
        throw new IdentifierMismatchException.IdentifierLengthException("{ table | view | procedure | function | trigger | event }", 3, name.Values.Count, name.Values.Select(x => x.Name));
    }

    public static ObjectIdentifier FromStrings(string catalog, string schema, string name, QuoteStyle quoteStyle = QuoteStyle.None)
    {
        return new ObjectIdentifier(name,
            new SchemaIdentifier(schema,
                new CatalogIdentifier(catalog, quoteStyle),
                quoteStyle),
            quoteStyle);
    }

    public static ObjectIdentifier FromStrings(IReadOnlyList<string> parts, SchemaIdentifier schema, QuoteStyle quoteStyle = QuoteStyle.None)
    {
        if (parts.Count == 3)
        {
            return FromStrings(parts[0], parts[1], parts[2], quoteStyle);
        }
        if (parts.Count == 2)
        {
            return FromStrings(schema.Catalog.Name, parts[0], parts[1], quoteStyle);
        }
        if (parts.Count == 1)
        {
            return FromStrings(schema.Catalog.Name, schema.Name, parts[0], quoteStyle);
        }
        throw new IdentifierMismatchException.IdentifierLengthException("{ table | view | procedure | function | trigger | event }", 3, parts.Count, parts);
    }

    public override string ToString()
    {
        return Schema + "." + base.ToString();
    }

    public string ToSimpleString()
    {
        return $"{Schema.Catalog.Name}.{Schema.Name}.{Name}";
    }
}
