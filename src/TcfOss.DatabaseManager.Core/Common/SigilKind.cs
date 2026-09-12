namespace TcfOss.DatabaseManager.Core.Common;

/// <summary>
/// Identifies the lexical sigil (prefix character) carried by an
/// <see cref="Identifier"/>. Plain identifiers use <see cref="None"/>.
/// </summary>
public enum SigilKind
{
    /// <summary>No prefix; an ordinary identifier.</summary>
    None,

    /// <summary>
    /// A local variable, written with a single <c>@</c> prefix
    /// (e.g. T-SQL <c>@x</c>). The <see cref="Identifier.Name"/> stores
    /// the bare name without the prefix.
    /// </summary>
    Variable,
}
