using System.Collections;
using System.Diagnostics.CodeAnalysis;
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
                ?? CreateInstance(listType, $"Unable to create list for deserializing Items for {nameof(DatabaseObjectDict<>)}");

        return CreateInstance(objectType, $"Unable to create instance of {objectType} for deserializing {nameof(DatabaseObjectDict<>)}", list, nameHandling);
    }

    [ExcludeFromCodeCoverage(Justification = "Concrete dictionary and List constructors cannot return null.")]
    private static object CreateInstance(Type type, string errorMessage, params object[] arguments)
    {
        return Activator.CreateInstance(type, arguments) ?? throw new JsonException(errorMessage);
    }

    [ExcludeFromCodeCoverage(Justification = "Supported dictionaries always have a non-null _nameHandling field.")]
    private static string? GetNameHandling(object value)
    {
        FieldInfo? field = value.GetType().GetField("_nameHandling", BindingFlags.NonPublic | BindingFlags.Instance);
        object? nameHandling = field != null ? field.GetValue(value) : NameHandling.None;
        return nameHandling?.ToString();
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("NameHandling");
        writer.WriteValue(GetNameHandling(value));

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
