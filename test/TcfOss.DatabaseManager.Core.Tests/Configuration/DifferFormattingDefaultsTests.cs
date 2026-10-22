
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.Configuration;

public class DifferFormattingDefaultsTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void DifferFormatting_ObjectNamePrefixWithSchema(bool? given, bool expected)
    {
        var rawConfig = GetRawConfig();
        rawConfig.DifferFormatting = new ConfigParsing.DifferFormattingSettings()
        {
            ObjectNamePrefixWithSchema = given
        };

        var config = GetConfig(rawConfig);
        Assert.Equal(expected, config.DifferFormatting.ObjectNamePrefixWithSchema);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, true)]
    public void DifferFormatting_OmitModifiersIfDefault(bool? given, bool expected)
    {
        var rawConfig = GetRawConfig();
        rawConfig.DifferFormatting = new ConfigParsing.DifferFormattingSettings()
        {
            OmitModifiersIfDefault = given
        };

        var config = GetConfig(rawConfig);
        Assert.Equal(expected, config.DifferFormatting.OmitModifiersIfDefault);
    }

    private static void SetLevel(ConfigParsing.DifferFormattingSettings raw, string propertyName, bool value)
    {
        typeof(ConfigParsing.DifferFormattingSettings).GetProperty(propertyName)!.SetValue(raw, value);
    }

    [Theory]
    [MemberData(nameof(BuildPreferRawTextInScriptCases))]
    public void DifferFormatting_PreferRawTextInScript(
        string objectType,
        bool? direct,
        bool? inScriptBase,
        bool? objectBase,
        bool? generalBase,
        bool expected)
    {
        var raw = new ConfigParsing.DifferFormattingSettings();
        if (direct != null)
        {
            SetLevel(raw, $"{objectType}PreferRawTextInScript", direct.Value);
        }
        if (inScriptBase != null)
        {
            SetLevel(raw, "PreferRawTextInScript", inScriptBase.Value);
        }
        if (objectBase != null)
        {
            SetLevel(raw, $"{objectType}PreferRawText", objectBase.Value);
        }
        if (generalBase != null)
        {
            SetLevel(raw, "PreferRawText", generalBase.Value);
        }
        var rawConfig = GetRawConfig();
        rawConfig.DifferFormatting = raw;

        var config = GetConfig(rawConfig);

        var actual = (bool)typeof(DifferFormattingSettings).GetProperty($"{objectType}PreferRawTextInScript")!.GetValue(config.DifferFormatting)!;

        Assert.Equal(expected, actual);
    }

    public static TheoryData<string, bool?, bool?, bool?, bool?, bool> BuildPreferRawTextInScriptCases()
    {
        var data = new TheoryData<string, bool?, bool?, bool?, bool?, bool>();
        var allBools = new bool?[] { true, false, null };

        foreach (var type in new[] { "Procedure", "Function", "Trigger", "View", "Event" })
        {
            var allCombinations = from direct in allBools
                                  from inScriptBase in allBools
                                  from objectBase in allBools
                                  from generalBase in allBools
                                  select new { direct, inScriptBase, objectBase, generalBase };
            foreach (var combination in allCombinations)
            {
                bool expected = combination.direct ?? combination.inScriptBase ?? combination.objectBase ?? combination.generalBase ?? true;
                data.Add(type, combination.direct, combination.inScriptBase, combination.objectBase, combination.generalBase, expected);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(BuildPreferRawTextCases))]
    public void DifferFormatting_PreferRawText(
        string objectType,
        bool? direct,
        bool? generalBase,
        bool expected)
    {
        var raw = new ConfigParsing.DifferFormattingSettings();
        if (direct != null)
        {
            SetLevel(raw, $"{objectType}PreferRawText", direct.Value);
        }
        if (generalBase != null)
        {
            SetLevel(raw, "PreferRawText", generalBase.Value);
        }
        var rawConfig = GetRawConfig();
        rawConfig.DifferFormatting = raw;

        var config = GetConfig(rawConfig);

        var actual = (bool)typeof(DifferFormattingSettings).GetProperty($"{objectType}PreferRawText")!.GetValue(config.DifferFormatting)!;

        Assert.Equal(expected, actual);
    }

    public static TheoryData<string, bool?, bool?, bool> BuildPreferRawTextCases()
    {
        var data = new TheoryData<string, bool?, bool?, bool>();
        var allBools = new bool?[] { true, false, null };

        foreach (var type in new[] { "Procedure", "Function", "Trigger", "View", "Event" })
        {
            var allCombinations = from direct in allBools
                                  from generalBase in allBools
                                  select new { direct, generalBase };
            foreach (var combination in allCombinations)
            {
                bool expected = combination.direct ?? combination.generalBase ?? true;
                data.Add(type, combination.direct, combination.generalBase, expected);
            }
        }
        return data;
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, true)]
    public void DifferFormatting_TerminateStatements(bool? given, bool expected)
    {
        var rawConfig = GetRawConfig();
        rawConfig.DifferFormatting = new ConfigParsing.DifferFormattingSettings()
        {
            TerminateStatements = given
        };

        var config = GetConfig(rawConfig);
        Assert.Equal(expected, config.DifferFormatting.TerminateStatements);
    }

    protected virtual bool UseDelimiterAroundProgramsDefault => false;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void DifferFormatting_UseDelimiterAroundPrograms(bool? given)
    {
        var rawConfig = GetRawConfig();
        rawConfig.DifferFormatting = new ConfigParsing.DifferFormattingSettings()
        {
            UseDelimiterAroundPrograms = given
        };

        var config = GetConfig(rawConfig);
        Assert.Equal(given ?? UseDelimiterAroundProgramsDefault, config.DifferFormatting.UseDelimiterAroundPrograms);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void DifferFormatting_UseDelimiterAroundViews(bool? given, bool expected)
    {
        var rawConfig = GetRawConfig();
        rawConfig.DifferFormatting = new ConfigParsing.DifferFormattingSettings()
        {
            UseDelimiterAroundViews = given
        };

        var config = GetConfig(rawConfig);
        Assert.Equal(expected, config.DifferFormatting.UseDelimiterAroundViews);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void DifferFormatting_EmitCommentsWithWeights(bool? given, bool expected)
    {
        var rawConfig = GetRawConfig();
        rawConfig.DifferFormatting = new ConfigParsing.DifferFormattingSettings()
        {
            EmitCommentsWithWeights = given
        };

        var config = GetConfig(rawConfig);
        Assert.Equal(expected, config.DifferFormatting.EmitCommentsWithWeights);
    }

    public virtual ConfigParsing.Config GetRawConfig()
    {
        return TestDataRawConfig.MyTestRawConfig;
    }

    public virtual ConfigBase GetConfig(ConfigParsing.Config rawConfig)
    {
        var config = new GenericConfigLoader(new LoggerFactory().CreateLogger<GenericConfigLoader>())
            .LoadConfig("/home/username/database", rawConfig, []);
        return config;
    }
}
