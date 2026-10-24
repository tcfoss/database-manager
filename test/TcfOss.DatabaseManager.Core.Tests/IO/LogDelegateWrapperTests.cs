using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Tests.IO;

public class LogDelegateWrapperTests
{
    [Fact]
    public void ExecutionLogWrapper_Selector_IsDeferredAndRunsOnEveryToString()
    {
        int invocationCount = 0;
        var wrapper = new LogDelegateExecutionWrapper<int>(42, source =>
        {
            invocationCount++;
            return $"Selected {source}";
        });

        Assert.Equal(0, invocationCount);
        Assert.Equal("Selected 42", wrapper.ToString());
        Assert.Equal(1, invocationCount);
        Assert.Equal("Selected 42", wrapper.ToString());
        Assert.Equal(2, invocationCount);
    }

    [Fact]
    public void ExecutionLogWrapper_NullSource_WithSelector_InvokesSelector()
    {
        var wrapper = new LogDelegateExecutionWrapper<string?>(null, source =>
        {
            Assert.Null(source);
            return source ?? "Selected null";
        });

        Assert.Equal("Selected null", wrapper.ToString());
    }

    [Fact]
    public void ExecutionLogWrapper_SelectorReturnsNull_DoesNotFallBackToSource()
    {
        var wrapper = new LogDelegateExecutionWrapper<string>("Sensitive source", _ => null!);

        Assert.Null(wrapper.ToString());
    }

    [Fact]
    public void ExecutionLogWrapper_SelectorThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Selector failed");
        var wrapper = new LogDelegateExecutionWrapper<int>(42, _ => throw exception);

        Assert.Same(exception, Assert.Throws<InvalidOperationException>(wrapper.ToString));
    }

    [Theory]
    [InlineData("Source", "Source")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ExecutionLogWrapper_NoSelector_UsesSourceOrEmptyString(string? source, string expected)
    {
        var wrapper = new LogDelegateExecutionWrapper<string?>(source, null);

        Assert.Equal(expected, wrapper.ToString());
    }

    [Fact]
    public void ExecutionLogWrapper_NoSelector_ValueType_UsesToString()
    {
        var wrapper = new LogDelegateExecutionWrapper<int>(42, null);

        Assert.Equal("42", wrapper.ToString());
    }

    [Fact]
    public void ExecutionLogWrapper_SourceToStringReturnsNull_UsesEmptyString()
    {
        var wrapper = new LogDelegateExecutionWrapper<NullStringSource>(new NullStringSource(), null);

        Assert.Equal(string.Empty, wrapper.ToString());
    }

    [Fact]
    public void ExecutionLogWrapper_DefaultValue_UsesEmptyString()
    {
        var wrapper = default(LogDelegateExecutionWrapper<string>);

        Assert.Equal(string.Empty, wrapper.ToString());
    }

    private sealed class NullStringSource
    {
        public override string? ToString() => null;
    }

    [Fact]
    public void SerializationLogWrapper_Minimal()
    {
        var myData = new MyTestObject();
        var wrapper = new LogDelegateSerializationWrapper<MyTestObject>(myData, false);
        var actual = wrapper.ToString();
        var expected = """
        {
          "Value": 42
        }
        """;
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void SerializationLogWrapper_Minimal_TransformValue()
    {
        var myData = new MyTestObject();
        var wrapper = new LogDelegateSerializationWrapper<MyTestObject>(myData, false, obj => obj with { Value = obj.Value * 2 });
        var actual = wrapper.ToString();
        var expected = """
        {
          "Value": 84
        }
        """;
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void SerializationLogWrapper_LogEverything()
    {
        var myData = new MyTestObject();
        var wrapper = new LogDelegateSerializationWrapper<MyTestObject>(myData, true);
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
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
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
