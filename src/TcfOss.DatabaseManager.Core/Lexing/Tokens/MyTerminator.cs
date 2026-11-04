namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class MyTerminator(string delimiter) : Token
{
    public string Delimiter { get; } = delimiter;
    public override int Length => Delimiter.Length;

    private bool Equals(MyTerminator? other)
    {
        return other != null && Delimiter == other.Delimiter;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as MyTerminator);
    }

    public override bool Equals(Token? other)
    {
        return Equals(other as MyTerminator);
    }

    public override int GetHashCode()
    {
        return Delimiter.GetHashCode();
    }
}
