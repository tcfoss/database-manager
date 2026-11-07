namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class OperatorToken(string value) : SymbolToken(value)
{
    public string Value { get; } = value;
}
