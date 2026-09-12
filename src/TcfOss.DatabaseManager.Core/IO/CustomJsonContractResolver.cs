using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Statements.Components;

using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.IO;

public class CustomJsonContractResolver(SerializationSettings? settings = null) : DefaultContractResolver
{
    private readonly SerializationSettings _settings = settings ?? new SerializationSettings()
    {
        OmitMetaData = true,
        OmitPreNonSql = false,
        OmitRawText = false,
        OmitSourceRef = true,
    };

    private bool ShouldOmitMetaData(JsonProperty property)
    {
        return _settings.OmitMetaData && property.PropertyType == typeof(MetaData) && property.PropertyName == "Meta";
    }

    private bool ShouldOmitNonSql(JsonProperty property)
    {
        return _settings.OmitPreNonSql && property.PropertyType == typeof(NonSql) && property.PropertyName == "PreNonSql";
    }

    private bool ShouldOmitRawText(JsonProperty property)
    {
        return _settings.OmitRawText && property.PropertyType == typeof(string) && property.PropertyName == "RawBodyText";
    }

    private bool ShouldOmitSourceRef(JsonProperty property)
    {
        if (!_settings.OmitSourceRef)
        {
            return false;
        }
        if (property.PropertyType != typeof(SourceRef) && property.PropertyType != typeof(SourceRef?))
        {
            return false;
        }
        return property.PropertyName == "SourceRef" || property.PropertyName == "Source";
    }

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        if (ShouldOmitMetaData(property))
        {
            property.ShouldSerialize = _ => false;
        }
        else if (ShouldOmitNonSql(property))
        {
            property.ShouldSerialize = _ => false;
        }
        else if (ShouldOmitRawText(property))
        {
            property.ShouldSerialize = _ => false;
        }
        else if (ShouldOmitSourceRef(property))
        {
            property.ShouldSerialize = _ => false;
        }
        return property;
    }
}
