using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.Tests.App;

public class EnvironmentVariableReaderTests
{
    [Theory]
    [InlineData("NAME", "value")]
    [InlineData("EMPTY", "")]
    public void GetValue_ExistingVariable_ReturnsValue(string name, string expected)
    {
        var reader = new TestEnvironmentReader(new Dictionary<string, string>
        {
            { "NAME", "value" },
            { "EMPTY", "" },
        });

        Assert.Equal(expected, reader.GetValue(name));
    }

    [Fact]
    public void GetValue_MissingVariable_Throws()
    {
        var reader = new TestEnvironmentReader([]);

        Assert.Throws<ConfigurationException.EnvironmentVariableNotSet>(() => reader.GetValue("MISSING"));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("plain text", "plain text")]
    [InlineData("${ENV:NAME}", "value")]
    [InlineData("prefix-${ENV:NAME}-suffix", "prefix-value-suffix")]
    [InlineData("${ENV:NAME}-${ENV:OTHER}", "value-second")]
    [InlineData("${ENV:NAME}${ENV:NAME}", "valuevalue")]
    [InlineData("before${ENV:EMPTY}after", "beforeafter")]
    [InlineData("${ENV:_NAME_2}", "valid name")]
    [InlineData("${ENV:SELF}-${ENV:OTHER}", "${ENV:SELF}-second")]
    [InlineData("${ENV:FIRST}-${ENV:SECOND}", "${ENV:SECOND}-${ENV:FIRST}")]
    [InlineData("${ENV:NAME}-${ENV:NESTED}-${ENV:OTHER}", "value-${ENV:NAME}-second")]
    [InlineData("${ENV:PASSWORD}", "$1\\${ENV:MISSING}")]
    [InlineData("${ENV:1NAME}", "${ENV:1NAME}")]
    [InlineData("${ENV:BAD-NAME}", "${ENV:BAD-NAME}")]
    [InlineData("${ENV:}", "${ENV:}")]
    [InlineData("${ENV:NAME", "${ENV:NAME")]
    [InlineData("${env:NAME}", "${env:NAME}")]
    [InlineData("${NAME}", "${NAME}")]
    public void SubstituteVariables_ReplacesOriginalMatchesOnly(string input, string expected)
    {
        var reader = new TestEnvironmentReader(new Dictionary<string, string>
        {
            { "NAME", "value" },
            { "OTHER", "second" },
            { "EMPTY", "" },
            { "_NAME_2", "valid name" },
            { "SELF", "${ENV:SELF}" },
            { "FIRST", "${ENV:SECOND}" },
            { "SECOND", "${ENV:FIRST}" },
            { "NESTED", "${ENV:NAME}" },
            { "PASSWORD", "$1\\${ENV:MISSING}" },
        });

        Assert.Equal(expected, reader.SubstituteVariables(input));
    }

    [Fact]
    public void SubstituteVariables_MissingVariable_Throws()
    {
        var reader = new TestEnvironmentReader([]);

        Assert.Throws<ConfigurationException.EnvironmentVariableNotSet>(() => reader.SubstituteVariables("${ENV:MISSING}"));
    }
}
