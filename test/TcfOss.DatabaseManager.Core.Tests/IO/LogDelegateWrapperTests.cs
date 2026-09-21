using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Tests.IO;

public class LogDelegateWrapperTests
{
    [Fact]
    public void LogWrapper_Minimal()
    {
        var myData = new MyTestObject();
        var wrapper = new LogDelegateWrapper<MyTestObject>(myData, false);
        var actual = wrapper.ToString();
        var expected = """
        {
          "Value": 42
        }
        """;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LogWrapper_Minimal_TransformValue()
    {
        var myData = new MyTestObject();
        var wrapper = new LogDelegateWrapper<MyTestObject>(myData, false, obj => obj with { Value = obj.Value * 2 });
        var actual = wrapper.ToString();
        var expected = """
        {
          "Value": 84
        }
        """;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LogWrapper_LogEverything()
    {
        var myData = new MyTestObject();
        var wrapper = new LogDelegateWrapper<MyTestObject>(myData, true);
        var actual = wrapper.ToString();
        var expected = """
        {
          "Value": 42,
          "Meta": {
            "PreNonSql": null,
            "PostNonSql": null,
            "Start": 10,
            "End": 20,
            "EndPlusTerminatorLength": null,
            "RawText": null
          },
          "PreNonSql": {
            "$type": "TcfOss.DatabaseManager.Core.Statements.Components.NonSql+Spaces, TcfOss.DatabaseManager.Core",
            "Number": 3
          },
          "RawBodyText": "My Raw Body",
          "SourceRef": {
            "SourceId": 2,
            "Start": 0,
            "End": 10
          },
          "Source": {
            "SourceId": 1,
            "Start": 0,
            "End": 10
          }
        }
        """;
        Assert.Equal(expected, actual);
    }

    // ReSharper disable UnusedMember.Local
    private record MyTestObject
    {
        public int Value { get; init; } = 42;
        public MetaData Meta { get; init; } = new() { Start = 10, End = 20 };
        public NonSql? PreNonSql { get; init; } = new NonSql.Spaces(3);
        public string? RawBodyText { get; init; } = "My Raw Body";
        public SourceRef? SourceRef { get; init; } = new SourceRef(2, 0, 10);
        public SourceRef Source { get; init; } = new(1, 0, 10);
    }
}
