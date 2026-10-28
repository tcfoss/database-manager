using System.Runtime.CompilerServices;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.Tests.ErrorTests;

public class DefensiveGuardExceptionTests
{
    private static int CurrentLine([CallerLineNumber] int lineNumber = 0) => lineNumber;

    private static void AssertLocationAndMessage(
        DefensiveGuardException exception,
        int expectedLine,
        string expectedMember,
        string areaLabel,
        string unexpectedValue)
    {
        Assert.Equal(expectedLine, exception.LineNumber);
        Assert.Equal(expectedMember, exception.MemberName);
        Assert.EndsWith("DefensiveGuardExceptionTests.cs", exception.FilePath);

        string expected = $"Internal error: unexpected value '{unexpectedValue}' encountered in area '{areaLabel}' "
            + $"(at {exception.FilePath}:{expectedLine} in {expectedMember}). This code path was believed to be unreachable; "
            + "please report this at https://github.com/tcfoss/database-manager/issues.";
        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void MinimalInvocation()
    {
        (DefensiveGuardException exception, int line) = (new DefensiveGuardException("minimal area", "minimal bad value"), CurrentLine());

        AssertLocationAndMessage(exception, line, nameof(MinimalInvocation), "minimal area", "minimal bad value");
        Assert.Null(exception.Justification);
        Assert.False(exception.ConsiderRemovalWhenTypeUnionsAreSupported);
    }

    [Fact]
    public void WithJustification_JustificationNotInMessage()
    {
        (DefensiveGuardException exception, int line) = (new DefensiveGuardException("justified area", "justified bad value", "internal note"), CurrentLine());

        AssertLocationAndMessage(exception, line, nameof(WithJustification_JustificationNotInMessage), "justified area", "justified bad value");
        Assert.Equal("internal note", exception.Justification);
        Assert.False(exception.ConsiderRemovalWhenTypeUnionsAreSupported);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConsiderRemoval_FromString(bool considerRemoval)
    {
        (DefensiveGuardException exception, int line) = (new DefensiveGuardException(considerRemoval, "removal area", "bad removal value", "justification"), CurrentLine());

        AssertLocationAndMessage(exception, line, nameof(ConsiderRemoval_FromString), "removal area", "bad removal value");
        Assert.Equal(considerRemoval, exception.ConsiderRemovalWhenTypeUnionsAreSupported);
        Assert.Equal("justification", exception.Justification);
    }

    [Theory]
    [InlineData(typeof(int), "System.Int32")]
    [InlineData(typeof(string), "System.String")]
    public void FromType(Type unexpectedType, string typeString)
    {
        (DefensiveGuardException exception, int line) = (new DefensiveGuardException("type area", unexpectedType, "justification"), CurrentLine());

        AssertLocationAndMessage(exception, line, nameof(FromType), "type area", typeString);
        Assert.Equal("justification", exception.Justification);
        Assert.False(exception.ConsiderRemovalWhenTypeUnionsAreSupported);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConsiderRemoval_FromType(bool considerRemoval)
    {
        (DefensiveGuardException exception, int line) = (new DefensiveGuardException(considerRemoval, "type removal area", typeof(Uri)), CurrentLine());

        AssertLocationAndMessage(exception, line, nameof(ConsiderRemoval_FromType), "type removal area", "System.Uri");
        Assert.Equal(considerRemoval, exception.ConsiderRemovalWhenTypeUnionsAreSupported);
        Assert.Null(exception.Justification);
    }

    [Fact]
    public void FromType_WithoutFullName_UsesName()
    {
        Type genericParameter = typeof(List<>).GetGenericArguments()[0];
        Assert.Null(genericParameter.FullName);

        (DefensiveGuardException exception, int line) = (new DefensiveGuardException("generic area", genericParameter), CurrentLine());

        AssertLocationAndMessage(exception, line, nameof(FromType_WithoutFullName_UsesName), "generic area", "T");
    }
}
