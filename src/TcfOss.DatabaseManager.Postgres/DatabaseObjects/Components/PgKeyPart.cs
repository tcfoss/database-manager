using System.Globalization;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public abstract record PgKeyPart
{
    public Direction? Direction { get; init; }
    public NullsOrder? NullsOrder { get; init; }
    public OperatorClassOptions? OperatorClass { get; init; }

    public abstract StatementKeyPart ToStatementKeyPart(bool removeOptionalElements);

    public record Column(Identifier Name) : PgKeyPart
    {
        public override StatementKeyPart ToStatementKeyPart(bool removeOptionalElements)
        {
            return new StatementKeyPart.Column(Name, Direction.Clean(removeOptionalElements))
            {
                OperatorClass = OperatorClass?.ToStatementOperatorClass(),
                NullsOrder = NullsOrder
            };
        }
    }

    public record IndexExpression(Expression Expression) : PgKeyPart
    {
        public override StatementKeyPart ToStatementKeyPart(bool removeOptionalElements)
        {
            return new StatementKeyPart.IndexExpression(Expression, Direction.Clean(removeOptionalElements))
            {
                OperatorClass = OperatorClass?.ToStatementOperatorClass(),
                NullsOrder = NullsOrder
            };
        }
    }

    public record OperatorClassOptions(ObjectName Name)
    {
        public int? SignatureLength { get; init; }
        public int? NumberOfRanges { get; init; }

        public PgStatementOperatorClass ToStatementOperatorClass()
        {
            SqlValueList<ObjectNameKvp> parameters = [];
            if (SignatureLength is { } signatureLength)
            {
                parameters.Add(new ObjectNameKvp(new ObjectName([new Identifier("siglen")]),
                    new Value.Number(signatureLength.ToString(CultureInfo.InvariantCulture), false)));
            }
            if (NumberOfRanges is { } numberOfRanges)
            {
                parameters.Add(new ObjectNameKvp(new ObjectName([new Identifier("numranges")]),
                    new Value.Number(numberOfRanges.ToString(CultureInfo.InvariantCulture), false)));
            }
            return new PgStatementOperatorClass(Name, parameters.Count == 0 ? null : parameters);
        }
    }
}
