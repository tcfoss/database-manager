namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public abstract class SymbolToken(string symbol) : Token
{
    private string Symbol { get; } = symbol;

    public override int Length => Symbol.Length;

    public override string ToString()
    {
        return Symbol;
    }

    private bool Equals(SymbolToken? other)
    {
        return other != null && Symbol == other.Symbol;
    }

    public override bool Equals(object? obj) => Equals(obj as SymbolToken);

    public override bool Equals(Token? other) => Equals(other as SymbolToken);

    public override int GetHashCode()
    {
        return Symbol.GetHashCode();
    }
}
