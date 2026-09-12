namespace TcfOss.DatabaseManager.Core.Configuration.Attributes;

public enum IdentifierQuotationHandling
{
    /// <summary>
    /// Always quote identifiers.
    /// </summary>
    Always,

    /// <summary>
    /// Quote identifiers if they contain non-standard characters or are reserved keywords. If
    /// they are not, leave them as they are in the source text.
    /// </summary>
    IfSpecial,

    /// <summary>
    /// Quote identifiers if they contain non-standard characters or are reserved keywords. If
    /// they are not, remove quotes.
    /// </summary>
    OnlyIfSpecial,

    /// <summary>
    /// Quote identifiers if they contain non-standard characters, are reserved keywords, or are
    /// built-in functions. If they are not, leave them as they are in the source text.
    /// </summary>
    IfSpecialOrFunction,

    /// <summary>
    /// Quote identifiers if they contain non-standard characters, are reserved keywords, or are
    /// built-in functions. If they are not, remove quotes.
    /// </summary>
    OnlyIfSpecialOrFunction,

    /// <summary>
    /// Leave identifiers as they are in the source text.
    /// </summary>
    LeaveOriginal,
}
