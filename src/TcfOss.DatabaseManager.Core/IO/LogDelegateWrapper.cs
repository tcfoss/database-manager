using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.Core.IO;

public readonly struct LogDelegateWrapper<T>(T objectToSerialize, bool logEverything, Func<T, T>? preSerializationTransform = null)
{
    private readonly bool _logEverything = logEverything;
    private readonly T _objectToSerialize = objectToSerialize;
    private readonly Func<T, T>? _preSerializationTransform = preSerializationTransform;

    public override string ToString()
    {
        T obj = _objectToSerialize;
        if (_preSerializationTransform != null)
        {
            obj = _preSerializationTransform(obj);
        }

        SerializationSettings settings;
        if (_logEverything)
        {
            settings = new SerializationSettings
            {
                OmitMetaData = false,
                OmitPreNonSql = false,
                OmitRawText = false,
                OmitSourceRef = false,
            };
        }
        else
        {
            settings = new SerializationSettings
            {
                OmitMetaData = true,
                OmitPreNonSql = true,
                OmitRawText = true,
                OmitSourceRef = true,
            };
        }

        return Serialization.ToJson(obj, settings);
    }
}
