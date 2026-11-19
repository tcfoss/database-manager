using System.Globalization;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public abstract record PgKeyPart
{
    public Direction? Direction { get; init; }
    public NullsOrder? NullsOrder { get; init; }
    public OperatorClassOptions? OperatorClass { get; init; }

    public abstract KeyPart ToStatementKeyPart();

    public record Column(Identifier Name) : PgKeyPart
    {
        public override KeyPart ToStatementKeyPart()
        {
            SqlValueList<ObjectNameKvp> parameters = GetIndexParameters();
            return new KeyPart.Column(Name, null, Direction)
            {
                PgOptions = GetIndexOptions(parameters)
            };
        }
    }

    public record IndexExpression(Expression Expression) : PgKeyPart
    {
        public override KeyPart ToStatementKeyPart()
        {
            SqlValueList<ObjectNameKvp> parameters = GetIndexParameters();
            return new KeyPart.IndexExpression(Expression, Direction)
            {
                PgOptions = GetIndexOptions(parameters)
            };
        }
    }

    public record OperatorClassOptions(ObjectName Name)
    {
        public int? SignatureLength { get; init; }
        public int? NumberOfRanges { get; init; }
    }

    protected SqlValueList<ObjectNameKvp> GetIndexParameters()
    {
        SqlValueList<ObjectNameKvp> parameters = [];
        if (OperatorClass?.SignatureLength is { } signatureLength)
        {
            parameters.Add(new ObjectNameKvp(new ObjectName([new Identifier("siglen")]),
                new Value.Number(signatureLength.ToString(CultureInfo.InvariantCulture), false)));
        }
        if (OperatorClass?.NumberOfRanges is { } numberOfRanges)
        {
            parameters.Add(new ObjectNameKvp(new ObjectName([new Identifier("numranges")]),
                new Value.Number(numberOfRanges.ToString(CultureInfo.InvariantCulture), false)));
        }
        return parameters;
    }

    protected PgKeyPartOptions? GetIndexOptions(SqlValueList<ObjectNameKvp> parameters)
    {
        if (OperatorClass != null || NullsOrder != null)
        {
            return new PgKeyPartOptions(OperatorClass?.Name, parameters.Count == 0 ? null : parameters, NullsOrder);
        }
        return null;
    }
}
