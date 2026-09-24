using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

#pragma warning disable IDE0060 // Remove unused parameter
// ReSharper disable UnusedMember.Global

namespace TcfOss.DatabaseManager.Core.IO.YamlConverters;

public class YamlFileInfoConverter
    : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(FileInfo);
    }

    public static object ReadYaml(IParser parser, Type type)
    {
        string value = parser.Consume<Scalar>().Value;
        return new FileInfo(value);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        return ReadYaml(parser, type);
    }

    public static void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value == null)
        {
            emitter.Emit(new Scalar("null"));
            return;
        }

        var fileInfo = (FileInfo)value;
        emitter.Emit(new Scalar(fileInfo.FullName));
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        WriteYaml(emitter, value, type);
    }
}
