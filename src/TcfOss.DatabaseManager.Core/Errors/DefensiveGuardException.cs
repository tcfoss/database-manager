using System.Runtime.CompilerServices;

namespace TcfOss.DatabaseManager.Core.Errors;

public class DefensiveGuardException(
    bool considerRemovalWhenTypeUnionsAreSupported,
    string areaLabel,
    string unexpectedValue,
    string? justification = null,
    [CallerLineNumber] int lineNumber = 0,
    [CallerFilePath] string filePath = "",
    [CallerMemberName] string memberName = "")
    : InvalidOperationException(GetMessage(areaLabel, unexpectedValue, lineNumber, filePath, memberName))
{
    public int LineNumber { get; } = lineNumber;
    public string FilePath { get; } = filePath;
    public string MemberName { get; } = memberName;
    public string? Justification { get; } = justification;
    public bool ConsiderRemovalWhenTypeUnionsAreSupported { get; } = considerRemovalWhenTypeUnionsAreSupported;

    public DefensiveGuardException(
        string areaLabel,
        string unexpectedValue,
        string? justification = null,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "")
        : this(false, areaLabel, unexpectedValue, justification, lineNumber, filePath, memberName)
    { }

    public DefensiveGuardException(
        bool considerRemovalWhenTypeUnionsAreSupported,
        string areaLabel,
        Type unexpectedType,
        string? justification = null,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "")
        : this(considerRemovalWhenTypeUnionsAreSupported, areaLabel, unexpectedType.FullName ?? unexpectedType.Name, justification, lineNumber, filePath, memberName)
    { }

    public DefensiveGuardException(
        string areaLabel,
        Type unexpectedType,
        string? justification = null,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "")
        : this(false, areaLabel, unexpectedType.FullName ?? unexpectedType.Name, justification, lineNumber, filePath, memberName)
    { }

    public const string RepositoryUrl = "https://github.com/tcfoss/database-manager";

    private static string GetMessage(string areaLabel, string unexpectedValue, int lineNumber, string filePath, string memberName)
    {
        return $"Internal error: unexpected value '{unexpectedValue}' encountered in area '{areaLabel}' "
            + $"(at {filePath}:{lineNumber} in {memberName}). This code path was believed to be unreachable; "
            + $"please report this at {RepositoryUrl}/issues.";
    }
}
