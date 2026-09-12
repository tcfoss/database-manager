using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

/// <summary>
/// An SQL keyword or an identifier.
/// </summary>
/// <param name="value">
/// The bare identifier text, excluding any quoting characters and any
/// <see cref="Common.SigilKind"/> prefix.
/// </param>
/// <param name="quoteStyle">Quoting style applied to <paramref name="value"/>.</param>
/// <param name="sigil">Lexical sigil prefix (e.g. <c>@</c> for a local variable).</param>
public sealed class Word(string value, QuoteStyle quoteStyle = QuoteStyle.None, SigilKind sigil = SigilKind.None) : Token
{
    public string Value { get; } = value;
    public QuoteStyle? QuoteStyle { get; } = quoteStyle;
    public SigilKind Sigil { get; } = sigil;
    public Keyword Keyword { get; } = quoteStyle == Common.QuoteStyle.None && sigil == SigilKind.None
        ? KeywordHelper.GetKeyword(value)
        : Keyword.undefined;

    public override int Length => Value.Length
        + (QuoteStyle != Common.QuoteStyle.None ? 2 : 0)
        + Sigil switch { SigilKind.Variable => 1, _ => 0 };

    public Identifier ToIdentifier(int sourceId)
    {
        return new Identifier(Value, QuoteStyle ?? Common.QuoteStyle.None, Sigil)
        {
            Source = new SourceRef(sourceId, Location.Position, Location.Position + Length)
        };
    }

    public override string ToString()
    {
        return Identifier.QuoteString(Value, QuoteStyle ?? Common.QuoteStyle.None, Sigil);
    }

    private bool Equals(Word? other)
    {
        return other != null && Value == other.Value && QuoteStyle == other.QuoteStyle && Keyword == other.Keyword && Sigil == other.Sigil;
    }

    public override bool Equals(Token? other) => Equals(other as Word);

    public override bool Equals(object? obj) => Equals(obj as Word);

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, QuoteStyle, Sigil);
    }
}
