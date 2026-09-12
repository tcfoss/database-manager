using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.IO.JsonConverters;

public partial class ObjectIdentifierConverter : JsonConverter<ObjectIdentifier>
{
    private static readonly Regex s_objectIdentifierRegex = GenerateObjectIdentifierRegex();

    public override ObjectIdentifier? ReadJson(JsonReader reader, Type objectType, ObjectIdentifier? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.Value == null || reader.TokenType == JsonToken.Null)
        {
            return null;
        }
        string value = (string)reader.Value;

        return Parse(value);
    }

    public override void WriteJson(JsonWriter writer, ObjectIdentifier? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        writer.WriteValue(value.ToString());
    }

    public static ObjectIdentifier Parse(string given)
    {
        Match match = s_objectIdentifierRegex.Match(given);
        if (!match.Success)
        {
            throw new JsonException($"Invalid ObjectIdentifier format: '{given}'. Expected format is 'catalog.schema.object'");
        }

        (string Value, QuoteStyle QuoteStyle) catalog = IdentifierConverter.ParseSingle(match.Groups[1].Value);
        (string Value, QuoteStyle QuoteStyle) schema = IdentifierConverter.ParseSingle(match.Groups[2].Value);
        (string Value, QuoteStyle QuoteStyle) objectName = IdentifierConverter.ParseSingle(match.Groups[3].Value);

        return new ObjectIdentifier(objectName.Value, new SchemaIdentifier(schema.Value, new CatalogIdentifier(catalog.Value, catalog.QuoteStyle), schema.QuoteStyle), objectName.QuoteStyle);
    }

    [GeneratedRegex(@"^((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))\.((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))\.((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))$")]
    private static partial Regex GenerateObjectIdentifierRegex();
}
