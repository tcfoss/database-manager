using System.Diagnostics.CodeAnalysis;
using TcfOss.DataStructures.Enums;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace TcfOss.DatabaseManager.Core.IO.YamlConverters;

#pragma warning disable IDE0060 // Remove unused parameter
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedParameter.Global
// ReSharper disable MemberCanBePrivate.Global

public class YamlStringEnumConverter<T> : IYamlTypeConverter
    where T : IStringEnum<T>
{
    public bool Accepts(Type type)
    {
        return typeof(T).IsAssignableFrom(type);
    }

    [ExcludeFromCodeCoverage]
    public object ReadYaml(IParser parser, Type type) => throw new NotImplementedException();

    [ExcludeFromCodeCoverage]
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) => throw new NotImplementedException();

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value == null)
        {
            emitter.Emit(new Scalar("null"));
            return;
        }

        if (value is T enumValue)
        {
            emitter.Emit(new Scalar(enumValue.ToString()!));
            return;
        }

        throw new InvalidOperationException($"Expected a value of type {typeof(T).Name} while writing YAML, but got {value.GetType().Name}.");
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        WriteYaml(emitter, value, type);
    }
}
