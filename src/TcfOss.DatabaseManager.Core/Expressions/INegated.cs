namespace TcfOss.DatabaseManager.Core.Expressions;

public interface INegated
{
    public bool Negated { get; init; }

    public string? NegatedText
    {
        get
        {
            return Negated ? "NOT " : null;
        }
    }
}
