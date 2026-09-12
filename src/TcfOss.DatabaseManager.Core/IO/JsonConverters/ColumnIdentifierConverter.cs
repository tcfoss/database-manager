using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.IO.JsonConverters;

public partial class ColumnIdentifierConverter : JsonConverter<ColumnIdentifier>
{
    private static readonly Regex s_columnIdentifierRegex = GenerateColumnIdentifierRegex();

    public override ColumnIdentifier? ReadJson(JsonReader reader, Type objectType, ColumnIdentifier? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.Value == null || reader.TokenType == JsonToken.Null)
        {
            return null;
        }
        string value = (string)reader.Value;

        return Parse(value);
    }

    public override void WriteJson(JsonWriter writer, ColumnIdentifier? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        writer.WriteValue(value.ToString());
    }

    public static ColumnIdentifier Parse(string given)
    {
        Match match = s_columnIdentifierRegex.Match(given);
        if (!match.Success)
        {
            throw new JsonException($"Invalid ColumnIdentifier format: '{given}'. Expected format is 'catalog.schema.object.column'");
        }

        (string Value, QuoteStyle QuoteStyle) catalog = IdentifierConverter.ParseSingle(match.Groups[1].Value);
        (string Value, QuoteStyle QuoteStyle) schema = IdentifierConverter.ParseSingle(match.Groups[2].Value);
        (string Value, QuoteStyle QuoteStyle) objectName = IdentifierConverter.ParseSingle(match.Groups[3].Value);
        (string Value, QuoteStyle QuoteStyle) columnName = IdentifierConverter.ParseSingle(match.Groups[4].Value);

        return new ColumnIdentifier(columnName.Value, new ObjectIdentifier(objectName.Value, new SchemaIdentifier(schema.Value, new CatalogIdentifier(catalog.Value, catalog.QuoteStyle), schema.QuoteStyle), objectName.QuoteStyle), columnName.QuoteStyle);
    }

    [GeneratedRegex(@"^((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))\.((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))\.((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))\.((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))$")]
    private static partial Regex GenerateColumnIdentifierRegex();
}
