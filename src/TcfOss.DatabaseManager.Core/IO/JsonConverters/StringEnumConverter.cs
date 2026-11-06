using System.Collections.Concurrent;
using Newtonsoft.Json;
using TcfOss.DataStructures.Enums;

namespace TcfOss.DatabaseManager.Core.IO.JsonConverters;

public class StringEnumConverterFactory : JsonConverter
{
    private static readonly ConcurrentDictionary<Type, JsonConverter> s_converters = new();

    public override bool CanConvert(Type objectType)
    {
        return objectType.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType
            && interfaceType.GetGenericTypeDefinition() == typeof(IStringEnum<>)
            && interfaceType.GenericTypeArguments[0] == objectType);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        return GetConverter(objectType).ReadJson(reader, objectType, existingValue, serializer);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        GetConverter(value.GetType()).WriteJson(writer, value, serializer);
    }

    private static JsonConverter GetConverter(Type objectType)
    {
        return s_converters.GetOrAdd(objectType, type =>
            (JsonConverter)Activator.CreateInstance(typeof(StringEnumConverter<>).MakeGenericType(type))!);
    }
}

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
