using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsPrecedenceManager : PrecedenceManager
{
    protected override bool SupportsPostgresPredicateSyntax => false;

    public override short GetPrecedence(Precedence precedence)
    {
        return precedence switch
        {
            Precedence.UnarySign => 40,
            Precedence.Comparison => 19,
            Precedence.Like => 18,
            _ => base.GetPrecedence(precedence)
        };
    }
}
