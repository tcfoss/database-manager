using Newtonsoft.Json;
using TcfOss.DataStructures.Enums;

namespace TcfOss.DatabaseManager.Core.IO.JsonConverters;

public class StringEnumConverter<T> : JsonConverter<T> where T : IStringEnum<T>
{
    public override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.Value == null || reader.TokenType == JsonToken.Null)
        {
            return default;
        }

        string value = (string)reader.Value;

        return T.Parse(value);
    }

    public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteValue(value.ToString());
    }
}
