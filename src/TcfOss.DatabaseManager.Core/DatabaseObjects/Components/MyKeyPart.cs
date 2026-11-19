using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record MyKeyPart
{
    public abstract StatementKeyPart ToStatementKeyPart(bool removeOptionalElements);

    public record Column(Identifier Name, uint? Length = null, Direction? Direction = null) : MyKeyPart
    {
        public override StatementKeyPart ToStatementKeyPart(bool removeOptionalElements)
        {
            return new StatementKeyPart.Column(Name, Direction.Clean(removeOptionalElements))
            {
                Length = Length
            };
        }
    }

    public record IndexExpression(Expression Expression, Direction? Direction) : MyKeyPart
    {
        public override StatementKeyPart ToStatementKeyPart(bool removeOptionalElements)
        {
            return new StatementKeyPart.IndexExpression(Expression, Direction.Clean(removeOptionalElements));
        }
    }
}
