using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.IO.JsonConverters;

public class DatabaseObjectDictConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        if (!objectType.IsGenericType)
        {
            return false;
        }
        if (objectType.GetGenericTypeDefinition() != typeof(DatabaseObjectDict<>))
        {
            return false;
        }
        return true;
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        var jsonObject = JObject.Load(reader);
        NameHandling nameHandling = jsonObject["NameHandling"]?.ToObject<NameHandling>(serializer) ?? NameHandling.None;

        Type valueType = objectType.GetGenericArguments()[0];
        Type kvpType = typeof(KeyValuePair<,>).MakeGenericType(typeof(ObjectHandle), valueType);
        Type listType = typeof(List<>).MakeGenericType(kvpType);


        JToken? itemsToken = jsonObject["Items"];

        object list = itemsToken?.ToObject(listType, serializer)
                ?? Activator.CreateInstance(listType)
                ?? throw new JsonException($"Unable to create list for deserializing Items for {nameof(DatabaseObjectDict<>)}");

        return Activator.CreateInstance(objectType, list, nameHandling)
                ?? throw new JsonException($"Unable to create instance of {objectType} for deserializing {nameof(DatabaseObjectDict<>)}");
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        // obtain the private _nameHandling field via reflection
        FieldInfo? nhField = value.GetType().GetField("_nameHandling", BindingFlags.NonPublic | BindingFlags.Instance);
        object? nhObj = nhField != null ? nhField.GetValue(value) : NameHandling.None;

        writer.WriteStartObject();
        writer.WritePropertyName("NameHandling");
        writer.WriteValue(nhObj?.ToString());

        writer.WritePropertyName("Items");
        writer.WriteStartArray();
        foreach (object kvp in (IEnumerable)value)
        {
            serializer.Serialize(writer, kvp);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
