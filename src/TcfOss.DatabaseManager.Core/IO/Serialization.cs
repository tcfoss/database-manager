using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions.Attributes;
using TcfOss.DatabaseManager.Core.IO.JsonConverters;
using TcfOss.DatabaseManager.Core.IO.YamlConverters;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;
using GenerationMode = TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes.GenerationMode;

namespace TcfOss.DatabaseManager.Core.IO;

public static partial class Serialization
{
    private static readonly JsonSerializerSettings s_serializerSettings = new()
    {
        Formatting = Newtonsoft.Json.Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        TypeNameHandling = TypeNameHandling.Auto,
        Converters =
        {
            new DatabaseObjectDictConverter(),
            new DatabaseComponentDictConverter(),
            new ColumnIdentifierConverter(),
            new ObjectIdentifierConverter(),
            new SchemaIdentifierConverter(),
            new IdentifierConverter(),
            new StringEnumConverter(),
            new StringEnumConverter<GenerationMode>(),
            new StringEnumConverter<BinaryOperator>(),
            new StringEnumConverter<UnaryOperator>(),
            new StringEnumConverter<ReferentialAction>(),
            new StringEnumConverter<SecurityContext>(),
            new StringEnumConverter<ViewAlgorithm>(),
            new StringEnumConverter<ViewCheckOption>(),
            new StringEnumConverter<TriggerTime>(),
            new StringEnumConverter<TriggerEvent>(),
            new StringEnumConverter<TriggerOrderPosition>(),
            new StringEnumConverter<AggregateQuantifier>(),
            new StringEnumConverter<DateTimeUnit>(),
            new StringEnumConverter<Direction>(),
            new StringEnumConverter<IndexMethod>(),
            new StringEnumConverter<RoutineParameterDirection>(),
            new StringEnumConverter<SqlDataRelation>(),
            new StringEnumConverter<HandlerAction>(),
            new StringEnumConverter<DuplicateTreatment>(),
            new StringEnumConverter<SetOperator>(),
            new StringEnumConverter<DroppableObject>(),
            new StringEnumConverter<SetQuantifier>(),
            new StringEnumConverter<UseObject>(),
            new StringEnumConverter<CreateOrLabel>(),
            new StringEnumConverter<MySqlNumericAttribute>(),
            new StringEnumConverter<SignalPropertyName>(),
            new StringEnumConverter<MatchAgainstModifier>(),
            new StringEnumConverter<DeallocatePrepareLabel>(),
            new StringEnumConverter<EventEnabledStatus>(),
        }
    };

    private static readonly IDeserializer s_yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .WithCaseInsensitivePropertyMatching()
            .WithEnforceNullability()
            .Build();

    public static string ToJson<T>(T obj)
    {
        string json = JsonConvert.SerializeObject(obj, s_serializerSettings);
        return json;
    }

    public static string ToJson<T>(T obj, SerializationSettings settings)
    {
        var newSettings = new JsonSerializerSettings()
        {
            Formatting = s_serializerSettings.Formatting,
            NullValueHandling = s_serializerSettings.NullValueHandling,
            TypeNameHandling = s_serializerSettings.TypeNameHandling,
            Converters = s_serializerSettings.Converters,
            ContractResolver = new CustomJsonContractResolver(settings),
        };
        string json = JsonConvert.SerializeObject(obj, newSettings);
        return json;
    }

    public static T FromJson<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json, s_serializerSettings)!;
    }

    public static T ParseConfig<T>(string text, string path)
    {
        try
        {
            T result = s_yamlDeserializer.Deserialize<T>(text);
            return result;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw GetParseException(ex, path, text);
        }
    }

    public static T ParseConfig<T>(TextReader reader, string path)
    {
        try
        {
            T result = s_yamlDeserializer.Deserialize<T>(reader);
            return result;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw GetParseException(ex, path, null);
        }
    }

    public static T ParseConfig<T>(string path)
    {
        using var reader = new StreamReader(path);
        return ParseConfig<T>(reader, path);
    }

    private static ConfigParseException GetParseException(YamlDotNet.Core.YamlException ex, string? path, string? text)
    {
        Location loc = new((int)ex.Start.Line, (int)ex.Start.Column, (int)ex.Start.Index);
        if (text == null && path != null)
        {
            text = File.ReadAllText(path);
        }

        if (text == null)
        {
            return new ConfigParseException(ex.Message, loc, ex);
        }

        ConfigParseException? betterEx = SearchForUsefulException(ex, loc, text);

        ConfigParseException baseException = betterEx ?? new ConfigParseException(ex.Message, loc, ex);

        return new ConfigParseException.WithText(baseException, path, text);
    }

    private static ConfigParseException? SearchForUsefulException(Exception ex, Location location, string text)
    {
        Exception? current = ex;

        (string? fieldName, int? valuePos) extractedData = ExtractFieldName(text, location.Line);

        string fieldName = extractedData.fieldName ?? "unknown";
        Location newLocation = location;
        if (extractedData.valuePos.HasValue)
        {
            newLocation = location with { Column = extractedData.valuePos.Value, Position = location.Position + (extractedData.valuePos.Value - location.Column) };
        }

        while (current != null)
        {
            Match match = s_propertyNotFoundRegex.Match(current.Message);
            if (match.Success)
            {
                string unknownProperty = match.Groups[1].Value;
                return new ConfigParseException.UnknownField(unknownProperty, location, current);
            }
            match = s_requestedValueNotFoundRegex.Match(current.Message);
            if (match.Success)
            {
                string requestedValue = match.Groups[1].Value;
                return new ConfigParseException.RequestedValueNotFound(fieldName, requestedValue, newLocation, current);
            }
            match = s_invalidBooleanRegex.Match(current.Message);
            if (match.Success)
            {
                string value = match.Groups[2].Value;
                return new ConfigParseException.InvalidValue(fieldName, value, newLocation, current);
            }
            match = s_invalidValueRegex.Match(current.Message);
            if (match.Success)
            {
                string value = match.Groups[2].Value;
                return new ConfigParseException.InvalidValue(fieldName, value, newLocation, current);
            }
            if (current.Message == "Strict nullability enforcement error." && text != null)
            {
                return new ConfigParseException.InvalidNull(fieldName, newLocation, current);
            }

            current = current.InnerException;
        }
        return null;
    }

    private static (string? fieldName, int? valuePos) ExtractFieldName(string text, int line)
    {
        string[] lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        if (line - 1 < lines.Length)
        {
            string lineText = lines[line - 1];
            int colonIndex = lineText.IndexOf(':');
            if (colonIndex != -1)
            {
                string fieldName = lineText.Substring(0, colonIndex).Trim();

                colonIndex++;
                while (colonIndex + 1 < lineText.Length && char.IsWhiteSpace(lineText[colonIndex]))
                {
                    colonIndex++;
                }
                int valuePos = colonIndex + 1;
                return (fieldName, valuePos);
            }
        }
        return (null, null);
    }

    public static string ToYaml(object obj)
    {
        SerializerBuilder serializerBuilder = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .WithTypeConverter(new YamlStringEnumConverter<ReferentialAction>())
            .WithTypeConverter(new YamlStringEnumConverter<SecurityContext>())
            .WithTypeConverter(new YamlStringEnumConverter<ViewAlgorithm>())
            .WithTypeConverter(new YamlStringEnumConverter<ViewCheckOption>())
            .WithTypeConverter(new YamlStringEnumConverter<SqlDataRelation>())
            .WithTypeConverter(new YamlStringEnumConverter<RoutineParameterDirection>())
            .WithTypeConverter(new YamlStringEnumConverter<EventEnabledStatus>())
            .WithTypeConverter(new YamlStringEnumConverter<IndexMethod>())
            .WithTypeConverter(new YamlStringEnumConverter<MySqlNumericAttribute>())
            .WithTypeConverter(new YamlCatalogIdentifierConverter())
            .WithTypeConverter(new YamlSchemaIdentifierConverter());
        ISerializer serializer = serializerBuilder.Build();
        return serializer.Serialize(obj);
    }

    private static readonly Regex s_propertyNotFoundRegex = GetPropertyNotFoundRegex();
    private static readonly Regex s_requestedValueNotFoundRegex = GetRequestedValueNotFoundRegex();
    private static readonly Regex s_invalidBooleanRegex = GetInvalidBooleanRegex();
    private static readonly Regex s_invalidValueRegex = GetInvalidValueRegex();

    [GeneratedRegex("Property '([^']+)' not found", RegexOptions.Compiled)]
    private static partial Regex GetPropertyNotFoundRegex();

    [GeneratedRegex("Requested value '([^']+)' was not found", RegexOptions.Compiled)]
    private static partial Regex GetRequestedValueNotFoundRegex();

    [GeneratedRegex("The value (['\"])([^'\"]+)\\1 is not a valid YAML Boolean", RegexOptions.Compiled)]
    private static partial Regex GetInvalidBooleanRegex();

    [GeneratedRegex("The input string (['\"])([^'\"]+)\\1 was not in a correct format", RegexOptions.Compiled)]
    private static partial Regex GetInvalidValueRegex();
}
