namespace TcfOss.DatabaseManager.Core.Errors;

public class DefensiveGuardException(bool considerRemovalWhenTypeUnionsAreSupported, string areaLabel, string unexpectedValue, string? justification = null)
    : InvalidOperationException(GetMessage(areaLabel, unexpectedValue, considerRemovalWhenTypeUnionsAreSupported, justification))
{
    public DefensiveGuardException(string areaLabel, string unexpectedValue, string? justification = null)
        : this(false, areaLabel, unexpectedValue, justification)
    { }

    public DefensiveGuardException(bool considerRemovalWhenTypeUnionsAreSupported, string areaLabel, Type unexpectedType, string? justification = null)
        : this(considerRemovalWhenTypeUnionsAreSupported, areaLabel, unexpectedType.FullName ?? unexpectedType.Name, justification)
    { }

    public DefensiveGuardException(string areaLabel, Type unexpectedType, string? justification = null)
        : this(false, areaLabel, unexpectedType.FullName ?? unexpectedType.Name, justification)
    { }

    private static string GetMessage(string areaLabel, string unexpectedValue, bool considerRemovalWhenTypeUnionsAreSupported, string? justification = null)
    {
        string removalNote = considerRemovalWhenTypeUnionsAreSupported ? " Consider removing this guard when type unions are supported." : string.Empty;
        string justificationNote = justification is not null ? $" Justification: {justification}" : string.Empty;
        return $"Unexpected value '{unexpectedValue}' encountered in area '{areaLabel}'.{removalNote}{justificationNote}";
    }
}
