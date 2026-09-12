using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core.Common;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace TcfOss.DatabaseManager.Core.IO.YamlConverters;

#pragma warning disable IDE0060 // Unused parameter
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedParameter.Global
// ReSharper disable MemberCanBePrivate.Global

public class YamlSchemaIdentifierConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(SchemaIdentifier);
    }

    [ExcludeFromCodeCoverage]
    public static object ReadYaml(IParser parser, Type type) => throw new NotImplementedException();

    [ExcludeFromCodeCoverage]
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) => throw new NotImplementedException();

    public static void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value == null)
        {
            emitter.Emit(new Scalar("null"));
            return;
        }

        if (value is SchemaIdentifier schemaIdentifier)
        {
            emitter.Emit(new Scalar(schemaIdentifier.ToString()));
            return;
        }

        throw new InvalidOperationException($"Expected a value of type {nameof(SchemaIdentifier.Name)} while writing YAML, but got {value.GetType().Name}.");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        WriteYaml(emitter, value, type);
    }
}
