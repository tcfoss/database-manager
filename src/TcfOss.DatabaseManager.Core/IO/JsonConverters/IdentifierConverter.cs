using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.IO.JsonConverters;

public partial class IdentifierConverter : JsonConverter<Identifier>
{
    private static readonly Regex s_identifierRegex = GenerateIdentifierRegex();

    public override Identifier? ReadJson(JsonReader reader, Type objectType, Identifier? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.Value == null || reader.TokenType == JsonToken.Null)
        {
            return null;
        }
        string value = (string)reader.Value;
        return Parse(value);
    }

    public override void WriteJson(JsonWriter writer, Identifier? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        writer.WriteValue(value.ToString());
    }

    public static Identifier Parse(string given)
    {
        Match match = s_identifierRegex.Match(given);
        if (!match.Success)
        {
            throw new JsonException($"Invalid Identifier format: '{given}'. Expected format is 'catalog.schema.object'");
        }

        (string Value, QuoteStyle QuoteStyle) name = ParseSingle(match.Groups[1].Value);
        return new Identifier(name.Value, name.QuoteStyle);
    }

    public static (string Value, QuoteStyle QuoteStyle) ParseSingle(string given)
    {
        QuoteStyle quoteStyle = given[0] switch
        {
            '`' => QuoteStyle.Backticks,
            '[' => QuoteStyle.Brackets,
            '"' => QuoteStyle.Ansi,
            _ => QuoteStyle.None,
        };
        if (quoteStyle != QuoteStyle.None)
        {
            given = given[1..^1]; // Remove the surrounding quotes
        }

        return (given, quoteStyle);
    }

    [GeneratedRegex(@"^((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w*))$")]
    private static partial Regex GenerateIdentifierRegex();
}
