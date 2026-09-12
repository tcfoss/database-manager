using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.IO.JsonConverters;

public partial class SchemaIdentifierConverter : JsonConverter<SchemaIdentifier>
{
    private static readonly Regex s_schemaIdentifierRegex = GenerateSchemaIdentifierRegex();

    public override SchemaIdentifier? ReadJson(JsonReader reader, Type objectType, SchemaIdentifier? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.Value == null || reader.TokenType == JsonToken.Null)
        {
            return null;
        }
        string value = (string)reader.Value;

        return Parse(value);
    }

    public override void WriteJson(JsonWriter writer, SchemaIdentifier? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        writer.WriteValue(value.ToString());
    }

    public static SchemaIdentifier Parse(string given)
    {
        Match match = s_schemaIdentifierRegex.Match(given);
        if (!match.Success)
        {
            throw new JsonException($"Invalid SchemaIdentifier format: '{given}'. Expected format is 'catalog.schema.object'");
        }

        (string Value, QuoteStyle QuoteStyle) catalog = IdentifierConverter.ParseSingle(match.Groups[1].Value);
        (string Value, QuoteStyle QuoteStyle) schema = IdentifierConverter.ParseSingle(match.Groups[2].Value);

        return new SchemaIdentifier(schema.Value, new CatalogIdentifier(catalog.Value, catalog.QuoteStyle), schema.QuoteStyle);
    }

    [GeneratedRegex(@"^((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))\.((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))$")]
    private static partial Regex GenerateSchemaIdentifierRegex();
}
