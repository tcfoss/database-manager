using System.Reflection;
using Newtonsoft.Json;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DataStructures.Enums;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.Core.Tests;

public static class StringEnumTestHelpers
{
    public static IEnumerable<TheoryDataRow<Type>> StringEnumTypes(Type assemblyAnchor)
    {
        foreach (Type type in assemblyAnchor.Assembly.GetTypes().OrderBy(type => type.FullName))
        {
            if (!type.IsAbstract && type.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType
                && interfaceType.GetGenericTypeDefinition() == typeof(IStringEnum<>)))
            {
                yield return new TheoryDataRow<Type>(type);
            }
        }
    }

    public static void AssertStringEnumRoundTrips(Type enumType)
    {
        MethodInfo write = typeof(Serialization).GetMethods().Single(method => method.Name == nameof(Serialization.ToJson)
            && method.GetParameters().Length == 1).MakeGenericMethod(enumType);
        MethodInfo writeWithSettings = typeof(Serialization).GetMethods().Single(method => method.Name == nameof(Serialization.ToJson)
            && method.GetParameters().Length == 2).MakeGenericMethod(enumType);
        MethodInfo read = typeof(Serialization).GetMethod(nameof(Serialization.FromJson))!.MakeGenericMethod(enumType);
        FieldInfo[] values = [.. enumType.GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.FieldType == enumType)];
        Assert.NotEmpty(values);
        foreach (FieldInfo field in values)
        {
            object value = field.GetValue(null)!;
            string json = (string)write.Invoke(null, [value])!;

            Assert.Equal(JsonConvert.SerializeObject(value.ToString()), json);
            Assert.Equal(json, (string)writeWithSettings.Invoke(null, [value, new SerializationSettings()])!);
            Assert.Equal(value, read.Invoke(null, [json]));
        }
        Assert.Equal("null", (string)write.Invoke(null, [null])!);
        Assert.Null(read.Invoke(null, ["null"]));
    }

    public class StringEnumSerializer<T> : IXunitSerializer
        where T : IStringEnum<T>
    {
        public string Serialize(object value)
        {
            string? stringValue;
            if (value is T enumLike && (stringValue = enumLike.ToString()) != null)
            {
                return stringValue;
            }
            throw new ArgumentException($"Cannot serialize value of type {value.GetType().FullName}");
        }

        public object Deserialize(Type type, string serializedValue)
        {
            if (type == typeof(T))
            {
                var field = typeof(T).GetFields().Select(f => f.GetValue(null)).FirstOrDefault(f => f?.ToString()?.Equals(serializedValue, StringComparison.OrdinalIgnoreCase) ?? false);
                return field is T enumLike ? enumLike : throw new ArgumentException($"Cannot deserialize value '{serializedValue}' to type {type.FullName}");
            }
            throw new ArgumentException($"Cannot deserialize to type {type.FullName}");
        }

        public bool IsSerializable(Type type, object? value, out string failureReason)
        {
            if (type == typeof(T) && value is T)
            {
                failureReason = string.Empty;
                return true;
            }
            failureReason = $"Type {type.FullName} is not supported by EnumLikeSerializer<{typeof(T).FullName}>";
            return false;
        }
    }

    public class StringEnumTestData<T> : IEnumerable<TheoryDataRow<string, T>>
        where T : IStringEnum<T>
    {
        protected virtual IEnumerable<TheoryDataRow<string, T>> ExtraRows => [];

        public virtual IEnumerator<TheoryDataRow<string, T>> GetEnumerator()
        {
            var fields = typeof(T).GetFields().Where(f => f.IsStatic && f.FieldType == typeof(T));
            foreach (var field in fields)
            {
                var value = (T?)field.GetValue(null);
                if (value != null)
                {
                    yield return new TheoryDataRow<string, T>(value.ToString()!, value);
                }
            }

            foreach (var row in ExtraRows)
            {
                yield return row;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
